using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransferService.Business.Abstract.Services;
using MoneyTransferService.Business.Abstract.Handlers;
using MoneyTransferService.Business.Abstract.BusinessRules;
using MoneyTransferService.Business.Abstract.Infrastructure;
using MoneyTransferService.Business.Concrete.Services;
using MoneyTransferService.Business.Concrete.Handlers;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Business.Concrete.BusinessRules;
using MoneyTransferService.Business.Options;

namespace MoneyTransferService.Business;

public static class BusinessServiceRegistration
{
    public static IServiceCollection RegisterBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Services
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransactionService, TransactionService>();

        // Business rules
        services.AddScoped<ITransferBusinessRules, TransferBusinessRules>();

        // Transaction handlers
        services.AddScoped<ITransferHandler, TransferHandler>();
        services.AddScoped<IDepositHandler, DepositHandler>();
        services.AddScoped<IWithdrawHandler, WithdrawHandler>();

        // Shared infrastructure
        services.AddScoped<TransactionFactory>();
        services.AddScoped<ConcurrencyRetryExecutor>();

        // Utilities
        services.AddScoped<IIbanGenerator, TrIBanGenerator>();
        services.AddScoped<IbanOptions>();
        services.AddValidatorsFromAssembly(typeof(BusinessServiceRegistration).Assembly);
        return services;
    }
}
