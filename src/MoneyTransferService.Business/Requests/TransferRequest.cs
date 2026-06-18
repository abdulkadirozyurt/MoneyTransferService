namespace MoneyTransferService.Business.Requests;

public sealed record TransferRequest(
    Guid SenderAccountId,
    Guid ReceiverAccountId,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);