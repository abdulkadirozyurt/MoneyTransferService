using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Abstract;

public interface ITransactionAuditRepository
{
    Task LogTransferAsync(Transaction transfer, string eventType, string? failureReason = null);
}
