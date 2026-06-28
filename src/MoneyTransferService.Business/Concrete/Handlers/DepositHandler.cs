using FluentValidation;
using MoneyTransferService.Business.Abstract.Handlers;
using MoneyTransferService.Business.Models;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Handlers;

/// <summary>
/// Handles deposit operations: validates input, resolves the target account,
/// applies the balance change, persists the transaction, and logs audit events.
/// </summary>
public sealed class DepositHandler(
    IUnitOfWork unitOfWork,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IValidator<DepositCommand> validator,
    ITransactionAuditRepository auditRepository,
    TransactionFactory transactionFactory,
    ConcurrencyRetryExecutor retryExecutor) : IDepositHandler
{
    public async Task<Transaction> HandleAsync(DepositCommand command, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(command, cancellationToken);

        return await retryExecutor.ExecuteWithRetryAsync(
            operation: ct => ExecuteDepositAsync(command, ct),
            createFailedTransaction: reason => transactionFactory.CreateFailedTransaction(new CreateFailedTransactionParameters(
                TransactionTypes.DEPOSIT,
                command.IdempotencyKey,
                command.Amount,
                command.CurrencyCode,
                reason)),
            findExistingTransaction: ct => GetExistingTransactionAsync(command.IdempotencyKey, ct),
            cancellationToken);
    }

    private async Task<Transaction> ExecuteDepositAsync(DepositCommand command, CancellationToken cancellationToken)
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
                    $"Account currency ({account.CurrencyCode}) does not match deposit currency ({command.CurrencyCode}).");
            }

            var transaction = transactionFactory.CreatePendingDeposit(command, account);

            await auditRepository.LogTransferAsync(transaction, AuditEventType.INITIATED);

            account.Deposit(command.Amount);
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

    private async Task ValidateAsync(DepositCommand command, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(command, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException("Invalid deposit request.", result.Errors);
        }
    }
}
