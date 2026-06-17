using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MoneyTransferService.Entities.Concrete;

public sealed class TransferAuditLog
{
    public TransferAuditLog()
    {
        Timestamp = DateTime.UtcNow;
    }

    [BsonId]
    public ObjectId Id { get; set; }
    public Guid TransferId { get; set; }
    public string EventType { get; set; } = string.Empty; // "Initiated" | "Completed" | "Failed"
    public string SenderAccountNumber { get; set; } = string.Empty;
    public string ReceiverAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime Timestamp { get; set; }
}