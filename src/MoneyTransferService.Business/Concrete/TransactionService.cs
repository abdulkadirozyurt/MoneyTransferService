using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete;

public class TransactionService(
    IUnitOfWork unitOfWork,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IValidator<TransferCommand> transferRequestValidator,
    ITransferBusinessRules transferBusinessRules,
    ITransactionAuditRepository auditRepository) : ITransactionService
{
    public async Task<Transaction> TransferAsync(TransferCommand request, CancellationToken cancellationToken = default)
    {
        await ValidateRequestAsync(request, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            Transaction? existingTransfer = await GetExistingTransferAsync(request, transactionRepository, cancellationToken);
            if (existingTransfer != null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return existingTransfer;
            }

            var transferAccounts = await GetTransferAccountsAsync(accountRepository, request, cancellationToken);

            await EnsureTransferCanBeCompletedAsync(request, transferAccounts);

            var transfer = CreatePendingTransfer(request, transferAccounts);

            await auditRepository.LogTransferAsync(transfer, AuditEventType.INITIATED);

            ApplyTransferBalanceChanges(request, transferAccounts, accountRepository);

            await CompleteTransferAsync(transfer, transactionRepository, cancellationToken);

            await SaveTransferAsync(request, transferAccounts, transfer, cancellationToken);

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

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await transactionRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        return transactions.OrderByDescending(transaction => transaction.CreatedAt);
    }

    private async Task SaveTransferAsync(
        TransferCommand request,
        TransferAccounts transferAccounts,
        Transaction transfer,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var failedTransfer = CreateFailedTransfer(
                request,
                "Optimistic concurrency version conflict during save.",
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount,
                transfer.Id);
            await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);
            throw new ConcurrencyException(failedTransfer.FailureReason!, ex);
        }
        catch (DbUpdateException ex)
        {
            var failedTransfer = CreateFailedTransfer(
                request,
                "Transfer could not be completed.",
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount,
                transfer.Id);
            await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);
            throw new TransferPersistenceException(failedTransfer.FailureReason!, ex);
        }
    }

    private static async Task CompleteTransferAsync(Transaction transfer, IRepository<Transaction> transcationRepository, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.COMPLETED;
        transfer.CompletedAt = DateTimeOffset.UtcNow;
        await transcationRepository.AddAsync(transfer, cancellationToken);
    }

    private async Task EnsureTransferCanBeCompletedAsync(TransferCommand request, TransferAccounts transferAccounts)
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
            var failedTransfer = CreateFailedTransfer(
                request,
                ex.Message,
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount);

            await auditRepository.LogTransferAsync(
                failedTransfer,
                AuditEventType.FAILED,
                failedTransfer.FailureReason);

            throw;
        }
    }

    private sealed record TransferAccounts(Account SenderAccount, Account ReceiverAccount);

    private async Task<TransferAccounts> GetTransferAccountsAsync(IRepository<Account> accountRepository, TransferCommand request, CancellationToken cancellationToken)
    {
        var senderAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(request.SenderAccountId, cancellationToken),
            AccountRole.SENDER,
            request.SenderAccountId);

        var receiverAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(request.ReceiverAccountId, cancellationToken),
            AccountRole.RECEIVER,
            request.ReceiverAccountId);

        return new TransferAccounts(senderAccount, receiverAccount);
    }

    // Idempotency check: If a transfer with the same IdempotencyKey already exists, return it instead of creating a new one.
    private static async Task<Transaction?> GetExistingTransferAsync(TransferCommand request, IRepository<Transaction> transferRepository, CancellationToken cancellationToken)
    {
        return await transferRepository.GetAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken);
    }

    private async Task ValidateRequestAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await transferRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Invalid transfer request.", validationResult.Errors);
        }
    }

    private static void ApplyTransferBalanceChanges(TransferCommand request, TransferAccounts transferAccounts, IRepository<Account> accountRepository)
    {
        transferAccounts.SenderAccount.Debit(request.Amount);
        transferAccounts.ReceiverAccount.Deposit(request.Amount);

        accountRepository.Update(transferAccounts.SenderAccount);
        accountRepository.Update(transferAccounts.ReceiverAccount);
    }

    private static Transaction CreatePendingTransfer(TransferCommand request, TransferAccounts transferAccounts)
    {
        return new Transaction
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderAccountId = request.SenderAccountId,
            SenderAccount = transferAccounts.SenderAccount,
            ReceiverAccountId = request.ReceiverAccountId,
            ReceiverAccount = transferAccounts.ReceiverAccount,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    private static Transaction CreateFailedTransfer(
        TransferCommand request,
        string failureReason,
        Account? senderAccount = null,
        Account? receiverAccount = null,
        Guid? transferId = null)
    {
        var transfer = new Transaction
        {
            Id = transferId ?? Guid.Empty,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderAccountId = request.SenderAccountId,
            ReceiverAccountId = request.ReceiverAccountId,
            IdempotencyKey = request.IdempotencyKey,
            Status = TransferStatus.FAILED,
            FailureReason = failureReason
        };

        if (senderAccount != null)
        {
            transfer.SenderAccount = senderAccount;
        }

        if (receiverAccount != null)
        {
            transfer.ReceiverAccount = receiverAccount;
        }

        return transfer;
    }
}


