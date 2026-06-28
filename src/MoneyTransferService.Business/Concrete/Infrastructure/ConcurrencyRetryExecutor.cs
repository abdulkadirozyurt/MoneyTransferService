using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Infrastructure;

/// <summary>
/// Encapsulates the optimistic concurrency retry loop with idempotency checks.
/// Reusable across Transfer, Deposit, and Withdraw handlers.
/// </summary>
public sealed class ConcurrencyRetryExecutor(ITransactionAuditRepository auditRepository)
{
    private const int MaxRetryAttempts = 3;

    /// <summary>
    /// Executes the given operation with retry logic for optimistic concurrency conflicts.
    /// </summary>
    /// <param name="operation">The core transactional operation to execute.</param>
    /// <param name="createFailedTransaction">Factory to create a failed Transaction for audit logging.</param>
    /// <param name="findExistingTransaction">Idempotency lookup to find an already-committed transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed Transaction.</returns>
    public async Task<Transaction> ExecuteWithRetryAsync(
        Func<CancellationToken, Task<Transaction>> operation,
        Func<string, Transaction> createFailedTransaction,
        Func<CancellationToken, Task<Transaction?>> findExistingTransaction,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetryAttempts)
            {
                continue; // Retry the operation
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var failedTransaction = createFailedTransaction(
                    "Optimistic concurrency version conflict during transaction.");

                await auditRepository.LogTransferAsync(
                    failedTransaction,
                    AuditEventType.FAILED,
                    failedTransaction.FailureReason);

                throw new ConcurrencyException(failedTransaction.FailureReason!, exception);
            }
            catch (DbUpdateException exception)
            {
                // Another request may have committed the same idempotency key first.
                var existingTransaction = await findExistingTransaction(cancellationToken);
                if (existingTransaction != null)
                {
                    return existingTransaction;
                }

                var failedTransaction = createFailedTransaction(
                    "Transaction could not be completed.");

                await auditRepository.LogTransferAsync(
                    failedTransaction,
                    AuditEventType.FAILED,
                    failedTransaction.FailureReason);

                throw new TransferPersistenceException(failedTransaction.FailureReason!, exception);
            }
        }

        throw new InvalidOperationException("Max concurrency retry attempts exceeded.");
    }
}
