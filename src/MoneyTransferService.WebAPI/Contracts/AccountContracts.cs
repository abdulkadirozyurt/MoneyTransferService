using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.WebAPI.Contracts;

public sealed record CreateAccountRequest(
    Guid? IndividualCustomerId,
    Guid? CorporateCustomerId,
    string CurrencyCode,
    decimal InitialBalance = 0);

public sealed record AccountResponse(
    Guid Id,
    string AccountNumber,
    string CurrencyCode,
    decimal Balance,
    string Status,
    Guid? IndividualCustomerId,
    Guid? CorporateCustomerId,
    DateTimeOffset CreatedAt)
{
    public static AccountResponse FromAccount(Account account)
    {
        return new AccountResponse(
            account.Id,
            account.Iban,
            account.CurrencyCode,
            account.Balance,
            account.Status,
            account.IndividualCustomerId,
            account.CorporateCustomerId,
            account.CreatedAt);
    }
}

public sealed record AccountBalanceResponse(
    Guid AccountId,
    string AccountNumber,
    string CurrencyCode,
    decimal Balance,
    string Status);
