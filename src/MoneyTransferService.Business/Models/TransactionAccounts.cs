using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Models;

/// <summary>
/// Holds the resolved sender and receiver accounts for a transfer operation.
/// </summary>
public sealed record TransactionAccounts(Account SenderAccount, Account ReceiverAccount);
