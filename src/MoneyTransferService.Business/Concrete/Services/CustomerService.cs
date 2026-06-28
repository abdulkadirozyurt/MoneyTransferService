using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Abstract.Services;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Services;

public class CustomerService(
    IUnitOfWork unitOfWork,
    IIndividualCustomerRepository individualCustomerRepository,
    ICorporateCustomerRepository corporateCustomerRepository) : ICustomerService
{
    public async Task<IndividualCustomer> CreateIndividualCustomerAsync(
        string email,
        string phoneNumber,
        string firstName,
        string lastName,
        string nationalIdentityNumber,
        CancellationToken cancellationToken = default)
    {
        ValidateCommonFields(email, phoneNumber);
        ValidateIndividualFields(firstName, lastName, nationalIdentityNumber);

        var customer = new IndividualCustomer
        {
            Email = email.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            NationalIdentityNumber = nationalIdentityNumber.Trim()
        };

        await individualCustomerRepository.AddAsync(customer, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new CustomerCreationException("Individual customer could not be created.", ex);
        }

        return customer;
    }

    public async Task<CorporateCustomer> CreateCorporateCustomerAsync(
        string email,
        string phoneNumber,
        string companyName,
        string taxNumber,
        string taxOffice,
        CancellationToken cancellationToken = default)
    {
        ValidateCommonFields(email, phoneNumber);
        ValidateCorporateFields(companyName, taxNumber, taxOffice);

        var customer = new CorporateCustomer
        {
            Email = email.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            CompanyName = companyName.Trim(),
            TaxNumber = taxNumber.Trim(),
            TaxOffice = taxOffice.Trim()
        };

        await corporateCustomerRepository.AddAsync(customer, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new CustomerCreationException("Corporate customer could not be created.", ex);
        }

        return customer;
    }

    public async Task<IndividualCustomer?> GetIndividualCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await individualCustomerRepository.GetByIdAsync(id, cancellationToken);

    public async Task<CorporateCustomer?> GetCorporateCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await corporateCustomerRepository.GetByIdAsync(id, cancellationToken);

    public async Task<IEnumerable<IndividualCustomer>> GetIndividualCustomersAsync(CancellationToken cancellationToken = default)
        => await individualCustomerRepository.GetAllAsync(cancellationToken);

    public async Task<IEnumerable<CorporateCustomer>> GetCorporateCustomersAsync(CancellationToken cancellationToken = default)
        => await corporateCustomerRepository.GetAllAsync(cancellationToken);

    private static void ValidateCommonFields(string email, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidCustomerRequestException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new InvalidCustomerRequestException("PhoneNumber is required.");
        }
    }

    private static void ValidateIndividualFields(string firstName, string lastName, string nationalIdentityNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidCustomerRequestException("FirstName is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidCustomerRequestException("LastName is required.");
        }

        if (string.IsNullOrWhiteSpace(nationalIdentityNumber))
        {
            throw new InvalidCustomerRequestException("NationalIdentityNumber is required.");
        }
    }

    private static void ValidateCorporateFields(string companyName, string taxNumber, string taxOffice)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new InvalidCustomerRequestException("CompanyName is required.");
        }

        if (string.IsNullOrWhiteSpace(taxNumber))
        {
            throw new InvalidCustomerRequestException("TaxNumber is required.");
        }

        if (string.IsNullOrWhiteSpace(taxOffice))
        {
            throw new InvalidCustomerRequestException("TaxOffice is required.");
        }
    }
}
