using MoneyTransferService.Core.Entities.Concrete;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Entities.Abstract;

public abstract class Customer : Entity
{
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public ICollection<Account> Accounts { get; set; } = [];
}
