using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete;

public class AccountService(
    IUnitOfWork unitOfWork,
    IAccountRepository accountRepository,
    IIndividualCustomerRepository individualCustomerRepository,
    ICorporateCustomerRepository corporateCustomerRepository,
    IIbanGenerator ibanGenerator) : IAccountService
{
    public async Task<Account> CreateAccountAsync(
        Guid? individualCustomerId,
        Guid? corporateCustomerId,
        string currencyCode,
        decimal initialBalance = 0,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(individualCustomerId, corporateCustomerId, currencyCode, initialBalance);

        if (!await OwnerExistsAsync(individualCustomerId, corporateCustomerId, cancellationToken))
        {
            throw new AccountOwnerNotFoundException("Customer not found.");
        }

        var account = new Account
        {
            Iban = ibanGenerator.GenerateIban(),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            Balance = initialBalance,
            Status = AccountStatus.ACTIVE,
            IndividualCustomerId = individualCustomerId,
            CorporateCustomerId = corporateCustomerId
        };

        await accountRepository.AddAsync(account, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new AccountCreationException("Account could not be created.", ex);
        }

        return account;
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await accountRepository.GetByIdAsync(id, cancellationToken);
    }

    private static void ValidateCreateRequest(
        Guid? individualCustomerId,
        Guid? corporateCustomerId,
        string currencyCode,
        decimal initialBalance)
    {
        if (individualCustomerId is null == corporateCustomerId is null)
        {
            throw new InvalidAccountRequestException("Exactly one customer owner must be provided.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            throw new InvalidAccountRequestException("CurrencyCode must be a 3-letter code.");
        }

        if (initialBalance < 0)
        {
            throw new InvalidAccountRequestException("InitialBalance cannot be negative.");
        }
    }

    private async Task<bool> OwnerExistsAsync(
        Guid? individualCustomerId,
        Guid? corporateCustomerId,
        CancellationToken cancellationToken)
    {
        if (individualCustomerId is Guid individualCustomerGuid)
        {
            return await individualCustomerRepository.GetByIdAsync(individualCustomerGuid, cancellationToken) is not null;
        }

        return await corporateCustomerRepository.GetByIdAsync(corporateCustomerId!.Value, cancellationToken) is not null;
    }    
}
