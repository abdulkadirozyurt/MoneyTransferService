using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.WebAPI.Contracts;

public sealed record CreateTransferRequest(
    Guid SenderAccountId,
    Guid ReceiverAccountId,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);

public sealed record TransactionResponse(
    Guid Id,
    Guid SenderAccountId,
    Guid ReceiverAccountId,
    decimal Amount,
    string CurrencyCode,
    string Status,
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
            transaction.SenderAccountId,
            transaction.ReceiverAccountId,
            transaction.Amount,
            transaction.CurrencyCode,
            transaction.Status,
            transaction.IdempotencyKey,
            transaction.Description,
            transaction.FailureReason,
            transaction.CompletedAt,
            transaction.CreatedAt);
    }
}
