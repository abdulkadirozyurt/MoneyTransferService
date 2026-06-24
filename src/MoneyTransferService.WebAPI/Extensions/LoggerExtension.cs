using Serilog;
using Serilog.Configuration;

namespace MoneyTransferService.WebAPI.Extensions;

public static class LoggerExtension
{
    public static void AddConsoleLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    public static WebApplicationBuilder ConfigureLogger(this WebApplicationBuilder builder)
    {
        Log.Information("Starting up the application...");
        var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override(nameof(Microsoft), Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override(nameof(Microsoft.EntityFrameworkCore), Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console()
            .Enrich.FromLogContext();

        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            loggerConfiguration.WriteTo.MongoDBBson(mongoConnectionString, collectionName: "ApplicationLogs");
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        builder.Host.UseSerilog();

        return builder;
    }
}