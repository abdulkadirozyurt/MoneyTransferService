namespace MoneyTransferService.Business.Requests;

public sealed record TransferCommand(
    Guid SenderAccountId,
    Guid ReceiverAccountId,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);