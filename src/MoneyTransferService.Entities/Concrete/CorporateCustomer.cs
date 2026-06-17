using MoneyTransferService.Entities.Abstract;

namespace MoneyTransferService.Entities.Concrete;

public sealed class CorporateCustomer : Customer
{
	public string CompanyName { get; set; } = default!;
	public string TaxNumber { get; set; } = default!;
	public string TaxOffice { get; set; } = default!;
}
