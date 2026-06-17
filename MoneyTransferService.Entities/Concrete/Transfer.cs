using MoneyTransferService.Core.Entities.Concrete;
using MoneyTransferService.Core.Enums;

namespace MoneyTransferService.Entities.Concrete;

public sealed class Transfer : Entity
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    public string? Description { get; set; }

    // prevents duplicate transfer requests in case of network issues or retries
    // unique index on IdempotencyKey should be created in the database
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? FailureReason { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Guid SenderAccountId { get; set; }
    public Account SenderAccount { get; set; }

    public Guid ReceiverAccountId { get; set; }
    public Account ReceiverAccount { get; set; }
}
