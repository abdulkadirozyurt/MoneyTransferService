using MoneyTransferService.Entities.Abstract;

namespace MoneyTransferService.Entities.Concrete;

public sealed class IndividualCustomer : Customer
{
	public string FirstName { get; set; } = default!;
	public string LastName { get; set; } = default!;
	public string NationalIdentityNumber { get; set; } = default!;
}
