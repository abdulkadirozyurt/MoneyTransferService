using Serilog;
using Scalar.AspNetCore;
using MoneyTransferService.Business;
using MoneyTransferService.DataAccess;
using MoneyTransferService.WebAPI.Constants;
using MoneyTransferService.WebAPI.Endpoints;
using MoneyTransferService.WebAPI.Extensions;
using MoneyTransferService.WebAPI.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MoneyTransferService.WebAPI.ExceptionHandling;
using MoneyTransferService.WebAPI.OpenApi;


LoggerExtension.AddConsoleLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.ConfigureLogger();
    builder.Services.AddOpenTelemetryServices(builder.Configuration, builder.Environment);
    builder.Services.AddHealthChecksServices(builder.Configuration);


    builder.Services.AddExceptionHandler<GlobalExceptionHandler>().AddProblemDetails();
    builder.Services.AddMemoryCache();
    builder.Services.RegisterDataAccessServices(builder.Configuration);
    builder.Services.RegisterBusinessServices(builder.Configuration);
    builder.Services.AddOpenApi(options =>
        options.AddOperationTransformer(new CorrelationIdOperationTransformer()));

    builder.Services.AddRateLimitingConfiguration();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseConfiguredSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseRateLimiter();


    // if (app.Environment.IsDevelopment())
    // {
    //     app.MapOpenApi();
    //     app.MapScalarApiReference();    
    // }

    app.MapOpenApi();
    app.MapScalarApiReference();

    // do not check any dependecy, only check whether asp.net core can create response or not
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    // it run all registered health checks above
    app.MapHealthChecks("/health/ready");

    app.UseHttpsRedirection();

    var api = app.MapGroup("/api")
        .RequireRateLimiting(RateLimitingPolicies.PUBLIC_API);

    api.MapAccountEndpoints();
    api.MapCustomerEndpoints();
    api.MapTransactionEndpoints();

    app.Run();
}
catch (Exception exception) when (exception is not Microsoft.Extensions.Hosting.HostAbortedException)
{

    Log.Fatal(exception, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
