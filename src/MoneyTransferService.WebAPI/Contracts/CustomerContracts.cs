using MoneyTransferService.Entities.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.WebAPI.Contracts;

public sealed record CreateIndividualCustomerRequest(
    string Email,
    string PhoneNumber,
    string FirstName,
    string LastName,
    string NationalIdentityNumber);

public sealed record CreateCorporateCustomerRequest(
    string Email,
    string PhoneNumber,
    string CompanyName,
    string TaxNumber,
    string TaxOffice);

public sealed record CustomerResponse(
    Guid Id,
    string CustomerType,
    string Email,
    string PhoneNumber,
    DateTimeOffset CreatedAt,
    string? FirstName = null,
    string? LastName = null,
    string? NationalIdentityNumber = null,
    string? CompanyName = null,
    string? TaxNumber = null,
    string? TaxOffice = null)
{
    public static CustomerResponse FromIndividualCustomer(IndividualCustomer customer)
    {
        return new CustomerResponse(
            customer.Id,
            "Individual",
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            customer.FirstName,
            customer.LastName,
            customer.NationalIdentityNumber);
    }

    public static CustomerResponse FromCorporateCustomer(CorporateCustomer customer)
    {
        return new CustomerResponse(
            customer.Id,
            "Corporate",
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            CompanyName: customer.CompanyName,
            TaxNumber: customer.TaxNumber,
            TaxOffice: customer.TaxOffice);
    }
}