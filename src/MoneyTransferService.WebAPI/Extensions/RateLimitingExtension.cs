using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using MoneyTransferService.WebAPI.Constants;
using MoneyTransferService.WebAPI.Diagnostics;

namespace MoneyTransferService.WebAPI.Extensions;

public static class RateLimitingExtension
{
    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Applies one shared rate limit to all /api endpoints.
            options.AddPolicy(RateLimitingPolicies.API, _ =>
                RateLimitPartition.GetSlidingWindowLimiter(RateLimitingPolicies.API, _ => new SlidingWindowRateLimiterOptions
                {                   // !!!!!!!!!!!!!

                
                    // Allows 1000 requests per minute, evaluated with 10-second sliding segments.
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                var logger = httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");

                logger.LogWarning(
                    "Rate limit rejected request on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}. TraceId: {TraceId}. RequestId: {RequestId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    StatusCodes.Status429TooManyRequests,
                    RequestDiagnosticsContext.GetCorrelationId(httpContext),
                    RequestDiagnosticsContext.GetTraceId(),
                    httpContext.TraceIdentifier);

                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "You have exceeded the allowed number of requests. Please try again later.",
                    Instance = httpContext.Request.Path,
                    Extensions =
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    }
                };
                if (RequestDiagnosticsContext.GetCorrelationId(httpContext) is { } correlationId)
                    problemDetails.Extensions.Add("correlationId", correlationId);

                await httpContext.Response.WriteAsJsonAsync(
                    problemDetails, 
                    JsonSerializerOptions.Web, 
                    contentType: "application/problem+json", 
                    cancellationToken);
            };

        });

        return services;
    }
}