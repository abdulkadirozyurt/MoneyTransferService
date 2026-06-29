namespace MoneyTransferService.Business.Abstract.Services;

public interface IAccountLockService
{
    Task<TResult> ExecuteWithAccountLocksAsync<TResult>(IEnumerable<string> ibans, Func<Task<TResult>> operation, CancellationToken cancellationToken = default);
}