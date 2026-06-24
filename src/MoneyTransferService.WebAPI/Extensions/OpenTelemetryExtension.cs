using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MoneyTransferService.WebAPI.Extensions;

public static class OpenTelemetryExtension
{
    public static IServiceCollection AddOpenTelemetryServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var openTelemetryConsoleExporterEnabled =
            configuration.GetValue<bool>("OpenTelemetry:ConsoleExporterEnabled");

        var openTelemetryOtlpExporterEnabled =
            configuration.GetValue<bool>("OpenTelemetry:OtlpExporterEnabled");

        var openTelemetryTraceGrpcEndpoint =
            configuration.GetValue<string>("OpenTelemetry:TraceGrpcEndpoint") ?? "http://localhost:4317";

        var openTelemetryMetricsHttpEndpoint =
            configuration.GetValue<string>("OpenTelemetry:MetricsHttpEndpoint") ?? "http://localhost:9090/api/v1/otlp/v1/metrics";

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: hostEnvironment.ApplicationName,
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
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSqlClientInstrumentation();

                if (openTelemetryConsoleExporterEnabled)
                {
                    tracing.AddConsoleExporter();
                }

                if (openTelemetryOtlpExporterEnabled)
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(openTelemetryTraceGrpcEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
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
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(openTelemetryMetricsHttpEndpoint);
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
                }
            });

        return services;
    }
}
