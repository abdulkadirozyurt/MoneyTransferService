using MongoDB.Driver;
using MoneyTransferService.Entities.Concrete;
using MoneyTransferService.DataAccess.Abstract;

namespace MoneyTransferService.DataAccess.Concrete;

public class TransferAuditRepository : ITransferAuditRepository
{
    private readonly IMongoCollection<TransferAuditLog> _collection;

    public TransferAuditRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TransferAuditLog>("TransferAuditLogs");
    }

    public async Task LogTransferAsync(Transfer transfer, string eventType, string? failureReason = null)
    {
        if (transfer == null)
            throw new ArgumentNullException(nameof(transfer));

        var auditLog = new TransferAuditLog
        {
            TransferId = transfer.Id,
            EventType = eventType,
            SenderAccountNumber = transfer.SenderAccount?.AccountNumber ?? string.Empty,
            ReceiverAccountNumber = transfer.ReceiverAccount?.AccountNumber ?? string.Empty,
            Amount = transfer.Amount,
            CurrencyCode = transfer.CurrencyCode ?? string.Empty,
            FailureReason = failureReason ?? transfer.FailureReason,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _collection.InsertOneAsync(auditLog);
    }
}
