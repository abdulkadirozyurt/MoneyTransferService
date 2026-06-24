using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MoneyTransferService.WebAPI.Diagnostics;
using Serilog.Context;

namespace MoneyTransferService.WebAPI.Middlewares;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>
    /// Resolves and validates the request correlation ID, exposes it to logs and
    /// downstream components, and returns it in the response header.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context.</param>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationIdResult = GetOrCreateCorrelationId(httpContext);
        var correlationId = correlationIdResult.CorrelationId;

        RequestDiagnosticsContext.SetCorrelationId(httpContext, correlationId);
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[RequestDiagnosticsContext.CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var correlationIdProperty = LogContext.PushProperty("CorrelationId", correlationId);
        using var requestIdProperty = LogContext.PushProperty("RequestId", httpContext.TraceIdentifier);
        using var traceIdProperty = PushTraceIdProperty();

        if (!correlationIdResult.IsValid)
        {
            logger.LogWarning(
                "Rejected request with an invalid {CorrelationIdHeaderName}. ValueCount: {CorrelationIdValueCount}. MaximumValueLength: {CorrelationIdMaximumValueLength}",
                RequestDiagnosticsContext.CorrelationIdHeaderName,
                correlationIdResult.ReceivedValueCount,
                correlationIdResult.MaximumReceivedValueLength);

            await WriteInvalidCorrelationIdResponseAsync(httpContext);
            return;
        }

        await next(httpContext);
    }

    /// <summary>
    /// Reads <c>X-Correlation-ID</c> from the request. A valid single value is
    /// preserved; a missing or empty value causes a new ID to be generated; an
    /// invalid or multi-value header produces an invalid result with a safe new ID.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context containing the request headers.</param>
    /// <returns>The correlation ID to use together with its validation metadata.</returns>
    private static CorrelationIdResult GetOrCreateCorrelationId(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(RequestDiagnosticsContext.CorrelationIdHeaderName, out var correlationIdValues))
        {
            return CorrelationIdResult.Valid(CreateCorrelationId());
        }

        if (correlationIdValues.Count == 0)
        {
            return CorrelationIdResult.Valid(CreateCorrelationId());
        }

        if (correlationIdValues.Count == 1)
        {
            var correlationId = correlationIdValues[0];

            if (string.IsNullOrEmpty(correlationId))
            {
                return CorrelationIdResult.Valid(CreateCorrelationId());
            }

            if (RequestDiagnosticsContext.IsValidCorrelationId(correlationId))
            {
                return CorrelationIdResult.Valid(correlationId);
            }
        }

        var maximumReceivedValueLength = correlationIdValues
            .Where(value => value is not null)
            .Select(value => value!.Length)
            .DefaultIfEmpty(0)
            .Max();

        return CorrelationIdResult.Invalid(
            CreateCorrelationId(),
            correlationIdValues.Count,
            maximumReceivedValueLength);
    }

    /// <summary>
    /// Adds the active OpenTelemetry trace ID to the Serilog log context when one
    /// is available.
    /// </summary>
    /// <returns>A disposable Serilog property scope, or <see langword="null"/> when no trace is active.</returns>
    private static IDisposable? PushTraceIdProperty()
    {
        var traceId = RequestDiagnosticsContext.GetTraceId();
        return traceId is null
            ? null
            : LogContext.PushProperty("TraceId", traceId);
    }

    /// <summary>
    /// Writes a safe <c>400 application/problem+json</c> response for an invalid
    /// incoming correlation ID.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context and response target.</param>
    private static async Task WriteInvalidCorrelationIdResponseAsync(HttpContext httpContext)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Title = "Invalid correlation ID.",
            Status = StatusCodes.Status400BadRequest,
            Detail =
                $"{RequestDiagnosticsContext.CorrelationIdHeaderName} must contain 1 to " +
                $"{RequestDiagnosticsContext.CorrelationIdMaxLength} letters, digits, dots, underscores, or hyphens.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] =
            RequestDiagnosticsContext.GetCorrelationId(httpContext);

        if (RequestDiagnosticsContext.GetTraceId() is { } traceId)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            JsonSerializerOptions.Web,
            contentType: "application/problem+json",
            cancellationToken: httpContext.RequestAborted);
    }

    /// <summary>
    /// Creates a new time-ordered UUID version 7 correlation ID without separators.
    /// </summary>
    /// <returns>A 32-character UUID version 7 string.</returns>
    private static string CreateCorrelationId()
    {
        return Guid.CreateVersion7().ToString("N");
    }

    private sealed record CorrelationIdResult(string CorrelationId, bool IsValid, int ReceivedValueCount, int MaximumReceivedValueLength)
    {
        /// <summary>
        /// Creates a successful correlation ID resolution result.
        /// </summary>
        /// <param name="correlationId">The accepted or newly generated correlation ID.</param>
        /// <returns>A result marked as valid.</returns>
        public static CorrelationIdResult Valid(string correlationId)
        {
            return new CorrelationIdResult(correlationId, true, 0, 0);
        }

        /// <summary>
        /// Creates a failed validation result while retaining a safe generated ID
        /// for logging and the error response.
        /// </summary>
        /// <param name="correlationId">The safe API-generated replacement correlation ID.</param>
        /// <param name="receivedValueCount">The number of values received in the header.</param>
        /// <param name="maximumReceivedValueLength">The longest received header value length.</param>
        /// <returns>A result marked as invalid.</returns>
        public static CorrelationIdResult Invalid(
            string correlationId,
            int receivedValueCount,
            int maximumReceivedValueLength)
        {
            return new CorrelationIdResult(
                correlationId,
                false,
                receivedValueCount,
                maximumReceivedValueLength);
        }
    }
}
