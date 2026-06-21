using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.WebAPI.Contracts;

public sealed record CreateTransferRequest(
    Guid SenderAccountId,
    Guid ReceiverAccountId,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);

public sealed record TransferResponse(
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
    public static TransferResponse FromTransfer(Transaction transfer)
    {
        return new TransferResponse(
            transfer.Id,
            transfer.SenderAccountId,
            transfer.ReceiverAccountId,
            transfer.Amount,
            transfer.CurrencyCode,
            transfer.Status,
            transfer.IdempotencyKey,
            transfer.Description,
            transfer.FailureReason,
            transfer.CompletedAt,
            transfer.CreatedAt);
    }
}
