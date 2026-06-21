using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface ITransactionService
{
    Task<Transaction> TransferAsync(TransferCommand request, CancellationToken cancellationToken = default);

    Task<Transaction?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Transaction>> GetTransferHistoryAsync(CancellationToken cancellationToken = default);
}
