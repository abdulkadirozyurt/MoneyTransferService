using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract;

public interface ICustomerService
{
    Task<IndividualCustomer> CreateIndividualCustomerAsync(
        string email,
        string phoneNumber,
        string firstName,
        string lastName,
        string nationalIdentityNumber,
        CancellationToken cancellationToken = default);

    Task<CorporateCustomer> CreateCorporateCustomerAsync(
        string email,
        string phoneNumber,
        string companyName,
        string taxNumber,
        string taxOffice,
        CancellationToken cancellationToken = default);

    Task<IndividualCustomer?> GetIndividualCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CorporateCustomer?> GetCorporateCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<IndividualCustomer>> GetIndividualCustomersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CorporateCustomer>> GetCorporateCustomersAsync(CancellationToken cancellationToken = default);
}