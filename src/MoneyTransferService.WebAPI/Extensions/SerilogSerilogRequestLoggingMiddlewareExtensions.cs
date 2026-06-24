using MoneyTransferService.WebAPI.Diagnostics;
using Serilog;
using Serilog.AspNetCore;

namespace MoneyTransferService.WebAPI.Extensions;

public static class SerilogSerilogRequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseConfiguredSerilogRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(ConfigureRequestLogging);
    }

    internal static void ConfigureRequestLogging(RequestLoggingOptions options)
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.EnrichDiagnosticContext = (diagnosticsContext, httpContext) =>
        {
            if (RequestDiagnosticsContext.GetCorrelationId(httpContext) is { } correlationId)
            {
                diagnosticsContext.Set("CorrelationId", correlationId);
            }

            diagnosticsContext.Set("RequestId", httpContext.TraceIdentifier);

            if (RequestDiagnosticsContext.GetTraceId() is { } traceId)
            {
                diagnosticsContext.Set("TraceId", traceId);
            }

            diagnosticsContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticsContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    }
}
