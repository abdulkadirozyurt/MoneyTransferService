using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract.BusinessRules;

public interface ITransferBusinessRules
{
    Account EnsureAccountExists(Account? account, string accountRole, string iban);

    void EnsureAccountIsActive(Account account, string accountRole);

    void EnsureCurrencyMatches(Account account, string accountRole, string currencyCode);

    void EnsureSufficientFunds(Account senderAccount, decimal amount);
}
