using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MoneyTransferService.WebAPI.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MongoDb"] = string.Empty,
                ["OpenTelemetry:ConsoleExporterEnabled"] = "false",
                ["OpenTelemetry:OtlpExporterEnabled"] = "false"
            });
        });
        builder.ConfigureServices(services =>
            services.AddTransient<IStartupFilter, TraceCaptureStartupFilter>());
    }
}
