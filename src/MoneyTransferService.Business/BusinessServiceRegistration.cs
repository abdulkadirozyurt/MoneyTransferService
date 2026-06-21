using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.BusinessRules;
using MoneyTransferService.Business.Concrete;

namespace MoneyTransferService.Business;

public static class BusinessServiceRegistration
{
    public static IServiceCollection RegisterBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ITransferBusinessRules, TransferBusinessRules>();
        services.AddValidatorsFromAssembly(typeof(BusinessServiceRegistration).Assembly);
        return services;
    }
}
