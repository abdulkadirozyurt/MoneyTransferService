using MongoDB.Driver;
using MoneyTransferService.Entities.Concrete;
using MoneyTransferService.DataAccess.Abstract;

namespace MoneyTransferService.DataAccess.Concrete;

public class TransactionAuditRepository : ITransactionAuditRepository
{
    private readonly IMongoCollection<TransactionAuditLog> _collection;

    public TransactionAuditRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TransactionAuditLog>("TransactionAuditLogs");
    }

    public async Task LogTransferAsync(Transaction transfer, string eventType, string? failureReason = null)
    {
        if (transfer == null)
            throw new ArgumentNullException(nameof(transfer));

        var auditLog = new TransactionAuditLog
        {
            TransactionId = transfer.Id,
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
