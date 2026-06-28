using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;
using MoneyTransferService.Entities.Concrete;
using Z.EntityFramework.Plus;

namespace MoneyTransferService.DataAccess.Concrete;

public sealed class AccountRepository(ApplicationDbContext context) : Repository<Account, ApplicationDbContext>(context), IAccountRepository
{
    /// <summary>
    /// Gets an account by id and asks the database to hold an update lock until the current transaction completes.
    /// </summary>
    public async Task<Account?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Accounts
                                    .WithHint(SqlServerTableHintFlags.UPDLOCK | SqlServerTableHintFlags.ROWLOCK)
                                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);




        //     return await context.Accounts.FromSqlInterpolated($"""
        //               SELECT * FROM Accounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}
        //          """)
        //     .SingleOrDefaultAsync(cancellationToken);        
    }

    /// <summary>
    /// Gets an account by IBAN and asks the database to hold an update lock until the current transaction completes.
    /// </summary>
    public async Task<Account?> GetByIbanForUpdateAsync(string iban, CancellationToken cancellationToken = default)
    {
        return await Context.Accounts
                                    .WithHint(SqlServerTableHintFlags.UPDLOCK | SqlServerTableHintFlags.ROWLOCK)
                                    .SingleOrDefaultAsync(a => a.Iban == iban, cancellationToken);
    }
}
