using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface ITransferBusinessRules
{
    Account EnsureAccountExists(Account? account, string accountRole, Guid accountId);

    void EnsureAccountIsActive(Account account, string accountRole);

    void EnsureCurrencyMatches(Account account, string accountRole, string currencyCode);

    void EnsureSufficientFunds(Account senderAccount, decimal amount);
}
