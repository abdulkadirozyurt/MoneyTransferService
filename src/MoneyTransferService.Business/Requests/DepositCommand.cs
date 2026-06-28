namespace MoneyTransferService.Business.Requests;

public sealed record DepositCommand(
    string AccountIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);
