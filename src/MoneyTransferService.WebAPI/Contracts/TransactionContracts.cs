using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.WebAPI.Contracts;

public sealed record CreateTransferRequest(
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);

public sealed record CreateDepositRequest(
    string AccountIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);

public sealed record CreateWithdrawRequest(
    string AccountIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);

public sealed record TransactionResponse(
    Guid Id,
    string? SenderIban,
    string? ReceiverIban,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string TransactionType,
    string IdempotencyKey,
    string? Description,
    string? FailureReason,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt)
{
    public static TransactionResponse FromTransaction(Transaction transaction)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.SenderIban,
            transaction.ReceiverIban,
            transaction.Amount,
            transaction.CurrencyCode,
            transaction.Status,
            transaction.TransactionType,
            transaction.IdempotencyKey,
            transaction.Description,
            transaction.FailureReason,
            transaction.CompletedAt,
            transaction.CreatedAt);
    }
}
