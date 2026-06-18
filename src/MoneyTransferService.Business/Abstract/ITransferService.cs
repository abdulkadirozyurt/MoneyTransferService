using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface ITransferService
{
    Task<Transfer> TransferAsync(
        Guid senderAccountId,
        Guid receiverAccountId,
        decimal amount,
        string currencyCode,
        string idempotencyKey,
        string? description = null,
        CancellationToken cancellationToken = default);
}
