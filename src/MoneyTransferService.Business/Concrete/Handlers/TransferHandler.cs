using FluentValidation;
using MoneyTransferService.Business.Abstract.Handlers;
using MoneyTransferService.Business.Abstract.BusinessRules;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Business.Models;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Handlers;

/// <summary>
/// Handles the complete lifecycle of a money transfer between two accounts.
/// Includes validation, idempotency checks, deadlock-safe account locking,
/// business rule enforcement, balance mutation, persistence, and audit logging.
/// </summary>
public sealed class TransferHandler(
    IUnitOfWork unitOfWork,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IValidator<TransferCommand> transferRequestValidator,
    ITransferBusinessRules transferBusinessRules,
    ITransactionAuditRepository auditRepository,
    TransactionFactory transactionFactory,
    ConcurrencyRetryExecutor retryExecutor) : ITransferHandler
{
    public async Task<Transaction> HandleAsync(TransferCommand request, CancellationToken cancellationToken = default)
    {
        await ValidateRequestAsync(request, cancellationToken);

        return await retryExecutor.ExecuteWithRetryAsync(
            operation: ct => ExecuteTransferAttemptAsync(request, ct),
            createFailedTransaction: reason => transactionFactory.CreateFailedTransaction(new CreateFailedTransactionParameters(
                TransactionTypes.TRANSFER,
                request.IdempotencyKey,
                request.Amount,
                request.CurrencyCode,
                reason)),
            findExistingTransaction: ct => GetExistingTransactionAsync(request.IdempotencyKey, ct),
            cancellationToken);
    }

    private async Task<Transaction> ExecuteTransferAttemptAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingTransaction = await GetExistingTransactionAsync(request.IdempotencyKey, cancellationToken);
            if (existingTransaction != null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return existingTransaction;
            }

            var transferAccounts = await GetTransferAccountsAsync(request, cancellationToken);

            await EnsureTransferCanBeCompletedAsync(request, transferAccounts);

            var transfer = transactionFactory.CreatePendingTransfer(request, transferAccounts);

            await auditRepository.LogTransferAsync(transfer, AuditEventType.INITIATED);

            ApplyTransferBalanceChanges(request, transferAccounts);

            transfer.Status = TransferStatus.COMPLETED;
            transfer.CompletedAt = DateTimeOffset.UtcNow;
            await transactionRepository.AddAsync(transfer, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await auditRepository.LogTransferAsync(transfer, AuditEventType.COMPLETED);

            return transfer;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureTransferCanBeCompletedAsync(TransferCommand request, TransactionAccounts transferAccounts)
    {
        transferBusinessRules.EnsureAccountIsActive(transferAccounts.SenderAccount, AccountRole.SENDER);
        transferBusinessRules.EnsureAccountIsActive(transferAccounts.ReceiverAccount, AccountRole.RECEIVER);
        transferBusinessRules.EnsureCurrencyMatches(transferAccounts.SenderAccount, AccountRole.SENDER, request.CurrencyCode);
        transferBusinessRules.EnsureCurrencyMatches(transferAccounts.ReceiverAccount, AccountRole.RECEIVER, request.CurrencyCode);

        try
        {
            transferBusinessRules.EnsureSufficientFunds(transferAccounts.SenderAccount, request.Amount);
        }
        catch (InsufficientFundsException ex)
        {
            var failedTransfer = transactionFactory.CreateFailedTransaction(new CreateFailedTransactionParameters(
                TransactionTypes.TRANSFER,
                request.IdempotencyKey,
                request.Amount,
                request.CurrencyCode,
                ex.Message,
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount));

            await auditRepository.LogTransferAsync(
                failedTransfer,
                AuditEventType.FAILED,
                failedTransfer.FailureReason);

            throw;
        }
    }

    private async Task<TransactionAccounts> GetTransferAccountsAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        // Locking by IBAN Order: Always lock the account with the smaller IBAN first to prevent deadlocks.
        var senderComesFirst = string.CompareOrdinal(request.SenderIban, request.ReceiverIban) < 0;

        var firstAccountIban = senderComesFirst ? request.SenderIban : request.ReceiverIban;
        var secondAccountIban = senderComesFirst ? request.ReceiverIban : request.SenderIban;

        var firstAccount = await accountRepository.GetByIbanForUpdateAsync(firstAccountIban, cancellationToken);
        var secondAccount = await accountRepository.GetByIbanForUpdateAsync(secondAccountIban, cancellationToken);

        var senderAccount = firstAccountIban == request.SenderIban ? firstAccount : secondAccount;
        var receiverAccount = firstAccountIban == request.ReceiverIban ? firstAccount : secondAccount;

        var sender = transferBusinessRules.EnsureAccountExists(senderAccount, AccountRole.SENDER, request.SenderIban);
        var receiver = transferBusinessRules.EnsureAccountExists(receiverAccount, AccountRole.RECEIVER, request.ReceiverIban);

        return new TransactionAccounts(sender, receiver);
    }

    private void ApplyTransferBalanceChanges(TransferCommand request, TransactionAccounts transferAccounts)
    {
        transferAccounts.SenderAccount.Debit(request.Amount);
        transferAccounts.ReceiverAccount.Deposit(request.Amount);

        accountRepository.Update(transferAccounts.SenderAccount);
        accountRepository.Update(transferAccounts.ReceiverAccount);
    }

    private async Task<Transaction?> GetExistingTransactionAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return await transactionRepository.GetAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    private async Task ValidateRequestAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await transferRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Invalid transfer request.", validationResult.Errors);
        }
    }
}
