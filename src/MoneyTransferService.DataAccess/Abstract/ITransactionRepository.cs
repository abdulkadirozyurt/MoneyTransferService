using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Abstract;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetHistoryAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
