using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Context;

namespace MoneyTransferService.DataAccess;

public static class DataAccessRegistrar
{
    public static IServiceCollection RegisterDataAccessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SqlServer"));
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
