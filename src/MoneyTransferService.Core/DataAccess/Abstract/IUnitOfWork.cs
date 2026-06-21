namespace MoneyTransferService.Core.DataAccess.Abstract;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
