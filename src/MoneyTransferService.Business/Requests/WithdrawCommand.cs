namespace MoneyTransferService.Business.Requests;

public sealed record WithdrawCommand(
    string AccountIban,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string? Description = null);
