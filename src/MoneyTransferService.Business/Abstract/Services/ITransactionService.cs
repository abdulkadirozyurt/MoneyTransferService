using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract.Services;

public interface ITransactionService
{
    Task<Transaction> DepositAsync(DepositCommand command, CancellationToken cancellationToken = default);

    Task<Transaction> WithdrawAsync(WithdrawCommand command, CancellationToken cancellationToken = default);

    Task<Transaction> TransferAsync(TransferCommand command, CancellationToken cancellationToken = default);

    Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Transaction>> GetTransactionHistoryAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
