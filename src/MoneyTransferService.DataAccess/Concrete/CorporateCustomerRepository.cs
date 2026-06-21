using MoneyTransferService.Core.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Concrete;

public sealed class CorporateCustomerRepository(ApplicationDbContext context) : Repository<CorporateCustomer, ApplicationDbContext>(context), ICorporateCustomerRepository
{
}
