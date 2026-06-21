using MoneyTransferService.Core.Entities.Concrete;
using MoneyTransferService.Core.Constants;

namespace MoneyTransferService.Entities.Concrete;

public sealed class Transaction : Entity
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public string Status { get; set; } = TransferStatus.PENDING;
    public string? Description { get; set; }

    // prevents duplicate transfer requests in case of network issues or retries
    // unique index on IdempotencyKey should be created in the database
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? FailureReason { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Guid SenderAccountId { get; set; }
    public Account SenderAccount { get; set; } = null!;

    public Guid ReceiverAccountId { get; set; }
    public Account ReceiverAccount { get; set; } = null!;
}
