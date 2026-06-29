using System.Collections.Concurrent;
using MoneyTransferService.Business.Abstract.Services;

namespace MoneyTransferService.Business.Concrete.Services;

public sealed class AccountLockService : IAccountLockService
{
    // This service is designed to provide a mechanism for locking accounts 
    // based on their IBANs to ensure that operations on the same account are executed in a thread-safe manner. 
    // It uses a ConcurrentDictionary to manage SemaphoreSlim instances for each unique IBAN, 
    // allowing multiple threads to safely acquire locks on different accounts while preventing concurrent access to the same account.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    // The ExecuteWithAccountLocksAsync method takes a collection of IBANs, an operation to execute, and an optional cancellation token.
    // It first normalizes and orders the IBANs to ensure consistent locking order, preventing deadlocks.
    // It then acquires locks for each IBAN in the ordered list, executes the provided
    // operation, and finally releases the locks in reverse order to maintain proper lock hierarchy.                
    public async Task<TResult> ExecuteWithAccountLocksAsync<TResult>(IEnumerable<string> ibans, Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var orderedIbans = ibans
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var acquiredLocks = new List<SemaphoreSlim>();

        try
        {
            foreach (var iban in orderedIbans)
            {
                // Get or create a semaphore for the IBAN, ensuring that only one thread can access the account at a time.
                var semaphore = _accountLocks.GetOrAdd(
                                iban,
                                _ => new SemaphoreSlim(1, 1));

                // Wait asynchronously to acquire the semaphore, allowing other threads to continue executing while waiting.
                await semaphore.WaitAsync(cancellationToken);

                // Keep track of the acquired locks to ensure they can be released later.
                acquiredLocks.Add(semaphore);
            }

            // Execute the provided operation while holding the locks, ensuring that no other thread can modify the accounts involved in the operation. 
            return await operation();
        }
        finally
        {
            // Release the acquired locks in reverse order to maintain proper lock hierarchy and prevent potential deadlocks.
            for (var i = acquiredLocks.Count - 1; i >= 0; i--)
            {
                acquiredLocks[i].Release();
            }
        }
    }
}

