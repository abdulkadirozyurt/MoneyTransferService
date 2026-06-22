using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MoneyTransferService.Business;
using MoneyTransferService.DataAccess;
using MoneyTransferService.WebAPI.Endpoints;
using MoneyTransferService.WebAPI.ExceptionHandling;
using MoneyTransferService.WebAPI.Middlewares;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

        if (!string.IsNullOrEmpty(mongoConnectionString))
        {
            loggerConfiguration.WriteTo.MongoDB(mongoConnectionString, collectionName: "ApplicationLogs");
        }
    });

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>().AddProblemDetails();
    builder.Services.RegisterDataAccessServices(builder.Configuration);
    builder.Services.RegisterBusinessServices(builder.Configuration);
    builder.Services.AddOpenApi();

    var openTelemetryConsoleExporterEnabled =
        builder.Configuration.GetValue<bool>("OpenTelemetry:ConsoleExporterEnabled");

    var openTelemetryOtlpExporterEnabled =
        builder.Configuration.GetValue<bool>("OpenTelemetry:OtlpExporterEnabled");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource =>
         {
             resource.AddService(
                 serviceName: builder.Environment.ApplicationName,
                 serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                 serviceInstanceId: Environment.MachineName
             );
         })
        .WithTracing(tracing =>
        {
            tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation();            

            if (openTelemetryConsoleExporterEnabled)
            {
                tracing.AddConsoleExporter();
            }

            if (openTelemetryOtlpExporterEnabled)
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("http://localhost:4317");
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (openTelemetryConsoleExporterEnabled)
            {
                metrics.AddConsoleExporter();
            }

            if (openTelemetryOtlpExporterEnabled)
            {
                metrics.AddOtlpExporter();
            }
        });

    var sqlConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");

    builder.Services.AddHealthChecks()
            .AddSqlServer(
                sqlConnectionString!,
                name: "SqlServer",
                timeout: TimeSpan.FromSeconds(3))
            .AddMongoDb(
                sp => new MongoClient(mongoConnectionString!),
                name: "MongoDb",
                timeout: TimeSpan.FromSeconds(3));


    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();

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

    app.UseExceptionHandler();


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





