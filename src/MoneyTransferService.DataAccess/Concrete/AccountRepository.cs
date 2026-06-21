using MoneyTransferService.Core.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Concrete;

public class AccountRepository(ApplicationDbContext context) : Repository<Account, ApplicationDbContext>(context), IAccountRepository
{
}
