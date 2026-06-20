using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Context;

namespace MoneyTransferService.DataAccess;

public static class DataAccessRegistrar
{
    public static IServiceCollection RegisterDataAccessServices(this IServiceCollection services, IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SqlServer"));
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var connectionString = configuration.GetConnectionString("MongoDb");
        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback default for safety
            connectionString = "mongodb://mongodb:27017/MoneyTransferDb";
        }

        var mongoUrl = new MongoUrl(connectionString);
        var databaseName = mongoUrl.DatabaseName ?? "MoneyTransferDb";

        services.AddSingleton<IMongoClient>(sp => new MongoClient(connectionString));

        services.AddScoped<IMongoDatabase>(sp =>
        {
            var mongoClient = sp.GetRequiredService<IMongoClient>();
            return mongoClient.GetDatabase(databaseName);
        });

        services.AddScoped<ITransferAuditRepository, TransferAuditRepository>();

        return services;
    }
}
