using MongoDB.Driver;

namespace MoneyTransferService.WebAPI.Extensions;

public static class HealthCheckExtension
{
    public static IServiceCollection AddHealthChecksServices(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConnectionString = configuration.GetConnectionString("SqlServer");
        var mongoConnectionString = configuration.GetConnectionString("MongoDb");

        services.AddHealthChecks()
            .AddSqlServer(
                sqlConnectionString!,
                name: "SqlServer",
                timeout: TimeSpan.FromSeconds(3))
            .AddMongoDb(
                sp => new MongoClient(mongoConnectionString!),
                name: "MongoDb",
                timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}