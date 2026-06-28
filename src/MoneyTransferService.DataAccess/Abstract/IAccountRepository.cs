using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Abstract;

public interface IAccountRepository : IRepository<Account>
{
    /// <summary>
    /// Gets an account by id and asks the database to hold an update lock until the current transaction completes.
    /// </summary>
    Task<Account?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an account by IBAN and asks the database to hold an update lock until the current transaction completes.
    /// </summary>
    Task<Account?> GetByIbanForUpdateAsync(string iban, CancellationToken cancellationToken = default);
}
