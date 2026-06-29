using MoneyTransferService.Business.Abstract.Handlers;
using MoneyTransferService.Business.Abstract.Services;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Services;

/// <summary>
/// Thin facade that delegates each operation to its dedicated handler.
/// Contains no business logic itself — only routing and simple queries.
/// </summary>
public class TransactionService(
    ITransferHandler transferHandler,
    IDepositHandler depositHandler,
    IWithdrawHandler withdrawHandler,
    ITransactionRepository transactionRepository) : ITransactionService
{
    public Task<Transaction> DepositAsync(DepositCommand command, CancellationToken cancellationToken = default)
        => depositHandler.HandleAsync(command, cancellationToken);

    public Task<Transaction> WithdrawAsync(WithdrawCommand command, CancellationToken cancellationToken = default)
        => withdrawHandler.HandleAsync(command, cancellationToken);

    public Task<Transaction> TransferAsync(TransferCommand command, CancellationToken cancellationToken = default)
        => transferHandler.HandleAsync(command, cancellationToken);

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await transactionRepository.GetByIdAsync(id, cancellationToken);

    public async Task<IEnumerable<Transaction>> GetTransactionHistoryAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await transactionRepository.GetHistoryAsync(pageNumber, pageSize, cancellationToken);
    }
}
