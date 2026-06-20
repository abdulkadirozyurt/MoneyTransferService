using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Concrete;

namespace MoneyTransferService.Business;

public static class BusinessServiceRegistration
{
    public static IServiceCollection RegisterBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ITransferBusinessRules, TransferBusinessRules>();
        services.AddValidatorsFromAssembly(typeof(BusinessServiceRegistration).Assembly);
        return services;
    }
}
