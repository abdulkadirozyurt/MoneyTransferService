using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface ITransferService
{
    Task<Transaction> TransferAsync(
        Guid senderAccountId,
        Guid receiverAccountId,
        decimal amount,
        string currencyCode,
        string idempotencyKey,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<Transaction?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Transaction>> GetTransferHistoryAsync(CancellationToken cancellationToken = default);
}
