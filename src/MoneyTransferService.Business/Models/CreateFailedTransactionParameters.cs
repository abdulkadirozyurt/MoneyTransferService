using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Models;

/// <summary>
/// Parameter record for creating failed transaction entities in TransactionFactory.
/// </summary>
public sealed record CreateFailedTransactionParameters(
    string TransactionType,
    string IdempotencyKey,
    decimal Amount,
    string CurrencyCode,
    string FailureReason,
    Account? SenderAccount = null,
    Account? ReceiverAccount = null);
