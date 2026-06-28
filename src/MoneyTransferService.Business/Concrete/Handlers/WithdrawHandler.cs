using FluentValidation;
using MoneyTransferService.Business.Abstract.Handlers;
using MoneyTransferService.Business.Abstract.BusinessRules;
using MoneyTransferService.Business.Models;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Handlers;

/// <summary>
/// Handles withdraw operations: validates input, resolves the target account,
/// checks sufficient funds, applies the balance change, persists the transaction,
/// and logs audit events.
/// </summary>
public sealed class WithdrawHandler(
    IUnitOfWork unitOfWork,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IValidator<WithdrawCommand> validator,
    ITransferBusinessRules transferBusinessRules,
    ITransactionAuditRepository auditRepository,
    TransactionFactory transactionFactory,
    ConcurrencyRetryExecutor retryExecutor) : IWithdrawHandler
{
    public async Task<Transaction> HandleAsync(WithdrawCommand command, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(command, cancellationToken);

        return await retryExecutor.ExecuteWithRetryAsync(
            operation: ct => ExecuteWithdrawAsync(command, ct),
            createFailedTransaction: reason => transactionFactory.CreateFailedTransaction(new CreateFailedTransactionParameters(
                TransactionTypes.WITHDRAW,
                command.IdempotencyKey,
                command.Amount,
                command.CurrencyCode,
                reason)),
            findExistingTransaction: ct => GetExistingTransactionAsync(command.IdempotencyKey, ct),
            cancellationToken);
    }

    private async Task<Transaction> ExecuteWithdrawAsync(WithdrawCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var existing = await GetExistingTransactionAsync(command.IdempotencyKey, cancellationToken);
            if (existing != null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return existing;
            }

            var account = await accountRepository.GetByIbanForUpdateAsync(command.AccountIban, cancellationToken)
                ?? throw new Exceptions.AccountNotFoundException($"Account with IBAN {command.AccountIban} not found.");

            if (account.Status != AccountStatus.ACTIVE)
            {
                throw new Exceptions.AccountNotActiveException($"Account is not active. Status: {account.Status}");
            }

            if (account.CurrencyCode != command.CurrencyCode)
            {
                throw new Exceptions.CurrencyMismatchException(
                    $"Account currency ({account.CurrencyCode}) does not match withdraw currency ({command.CurrencyCode}).");
            }

            try
            {
                transferBusinessRules.EnsureSufficientFunds(account, command.Amount);
            }
            catch (Exceptions.InsufficientFundsException ex)
            {
                var failedTransaction = transactionFactory.CreateFailedTransaction(new CreateFailedTransactionParameters(
                    TransactionTypes.WITHDRAW,
                    command.IdempotencyKey,
                    command.Amount,
                    command.CurrencyCode,
                    ex.Message,
                    SenderAccount: account));

                await auditRepository.LogTransferAsync(
                    failedTransaction,
                    AuditEventType.FAILED,
                    failedTransaction.FailureReason);

                throw;
            }

            var transaction = transactionFactory.CreatePendingWithdraw(command, account);

            await auditRepository.LogTransferAsync(transaction, AuditEventType.INITIATED);

            account.Debit(command.Amount);
            accountRepository.Update(account);

            transaction.Status = TransferStatus.COMPLETED;
            transaction.CompletedAt = DateTimeOffset.UtcNow;
            await transactionRepository.AddAsync(transaction, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await auditRepository.LogTransferAsync(transaction, AuditEventType.COMPLETED);

            return transaction;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Transaction?> GetExistingTransactionAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return await transactionRepository.GetAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    private async Task ValidateAsync(WithdrawCommand command, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(command, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException("Invalid withdraw request.", result.Errors);
        }
    }
}
