using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.BusinessRules;

public sealed class TransferBusinessRules : ITransferBusinessRules
{
    public Account EnsureAccountExists(Account? account, string accountRole, string iban)
    {
        if (account == null)
        {
            throw new AccountNotFoundException($"{accountRole} account with IBAN {iban} not found.");
        }

        return account;
    }

    public void EnsureAccountIsActive(Account account, string accountRole)
    {
        if (account.Status != AccountStatus.ACTIVE)
        {
            throw new AccountNotActiveException($"{accountRole} account is not active. Status: {account.Status}");
        }
    }

    public void EnsureCurrencyMatches(Account account, string accountRole, string currencyCode)
    {
        if (account.CurrencyCode != currencyCode)
        {
            throw new CurrencyMismatchException($"{accountRole} account currency ({account.CurrencyCode}) does not match transfer currency ({currencyCode}).");
        }
    }

    public void EnsureSufficientFunds(Account senderAccount, decimal amount)
    {
        if (senderAccount.Balance < amount)
        {
            throw new InsufficientFundsException("Insufficient funds in sender account.");
        }
    }
}
