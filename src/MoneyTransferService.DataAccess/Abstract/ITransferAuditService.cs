using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Abstract;

public interface ITransferAuditService
{
    Task LogTransferAsync(Transfer transfer, string eventType, string? failureReason = null);
}
