using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;
using Polly;
using Polly.Retry;

namespace MoneyTransferService.Business.Concrete.Infrastructure;

/// <summary>
/// Encapsulates the optimistic concurrency retry loop with idempotency checks.
/// Reusable across Transfer, Deposit, and Withdraw handlers.
/// </summary>
public sealed class ConcurrencyRetryExecutor(ITransactionAuditRepository auditRepository)
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Defines the retry policy for handling optimistic concurrency exceptions.
    /// </summary>
    private static readonly ResiliencePipeline<Transaction> RetryPipeline =
        new ResiliencePipelineBuilder<Transaction>()
            .AddRetry(new RetryStrategyOptions<Transaction>
            {
                MaxRetryAttempts = MaxAttempts - 1,
                ShouldHandle = new PredicateBuilder<Transaction>()
                    .Handle<DbUpdateConcurrencyException>(),
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true // Adds randomness to the delay to reduce contention. Add random delay offset.
            })
            .Build();

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
        try
        {
            return await RetryPipeline.ExecuteAsync(async token => await operation(token), cancellationToken);
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
            var existingTransaction = await findExistingTransaction(cancellationToken);
            if (existingTransaction != null)
                return existingTransaction;

            var failedTransaction = createFailedTransaction(
                "Transaction could not be completed.");

            await auditRepository.LogTransferAsync(
                failedTransaction,
                AuditEventType.FAILED,
                failedTransaction.FailureReason);

            throw new TransferPersistenceException(failedTransaction.FailureReason!, exception);
        }
    }
}
