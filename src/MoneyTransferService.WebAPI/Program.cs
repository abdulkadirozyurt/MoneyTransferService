using MoneyTransferService.Business;
using MoneyTransferService.DataAccess;
using MoneyTransferService.WebAPI.Endpoints;
using MoneyTransferService.WebAPI.ExceptionHandling;
using MoneyTransferService.WebAPI.Middlewares;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;


Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new CompactJsonFormatter())
                .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        var mongoConnectionString = context.Configuration.GetConnectionString("MongoDb");

        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", "MoneyTransferService.WebAPI")
            .WriteTo.Console(new CompactJsonFormatter());

        loggerConfiguration.WriteTo.MongoDB(mongoConnectionString, "ApplicationLogs");
    });


    builder.Services.AddExceptionHandler<GlobalExceptionHandler>().AddProblemDetails();

    builder.Services.RegisterDataAccessServices(builder.Configuration);
    builder.Services.RegisterBusinessServices(builder.Configuration);
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.EnrichDiagnosticContext = (diagnosticsContext, httpContext) =>
        {
            diagnosticsContext.Set("TraceId", httpContext.TraceIdentifier);
            diagnosticsContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticsContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    });

    // if (app.Environment.IsDevelopment())
    // {
    //     app.MapOpenApi();
    //     app.MapScalarApiReference();    
    // }

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseHttpsRedirection();

    var api = app.MapGroup("/api");

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





