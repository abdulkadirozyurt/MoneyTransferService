using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface IAccountService
{
    Task<Account> CreateAccountAsync(
        Guid? individualCustomerId,
        Guid? corporateCustomerId,
        string currencyCode,
        decimal initialBalance = 0,
        CancellationToken cancellationToken = default);

    Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
