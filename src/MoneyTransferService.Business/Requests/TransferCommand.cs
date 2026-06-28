namespace MoneyTransferService.Business.Requests;

public sealed record TransferCommand(
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);