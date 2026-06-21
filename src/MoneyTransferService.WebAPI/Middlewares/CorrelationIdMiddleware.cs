using Serilog.Context;

namespace MoneyTransferService.WebAPI.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = GetOrCreateCorrelationId(httpContext);

        httpContext.TraceIdentifier = correlationId;
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(httpContext);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }
        return Guid.NewGuid().ToString("N");
    }
}
