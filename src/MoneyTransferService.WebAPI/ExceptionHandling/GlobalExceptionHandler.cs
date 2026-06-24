using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.WebAPI.Diagnostics;

namespace MoneyTransferService.WebAPI.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Converts validation, business, and unexpected exceptions into standardized
    /// Problem Details responses containing correlation and trace identifiers.
    /// </summary>
    /// <param name="httpContext">The HTTP context whose response will be written.</param>
    /// <param name="exception">The exception raised while processing the request.</param>
    /// <param name="cancellationToken">Stops response writing when the request is cancelled.</param>
    /// <returns><see langword="true"/> because this handler produces the final error response.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            const int validationStatus = StatusCodes.Status400BadRequest;
            LogValidationException(httpContext, exception, validationStatus);

            var validationProblemDetails = new HttpValidationProblemDetails(
                validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = validationStatus,
                Title = "One or more validation errors occurred.",
                Detail = "Please refer to the errors property for additional details.",
                Instance = httpContext.Request.Path
            };

            AddDiagnosticIdentifiers(validationProblemDetails, httpContext);
            httpContext.Response.StatusCode = validationStatus;

            await httpContext.Response.WriteAsJsonAsync(
                validationProblemDetails,
                JsonSerializerOptions.Web,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);

            return true;
        }

        var (status, title, detail) = exception switch
        {
            BusinessException businessException => (
                (int)businessException.StatusCode,
                "Request failed.",
                businessException.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "Internal server error. Please try again later or contact support if the issue persists.")
        };

        LogException(httpContext, exception, status);

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        AddDiagnosticIdentifiers(problemDetails, httpContext);
        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            JsonSerializerOptions.Web,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    /// <summary>
    /// Writes a structured warning log for a FluentValidation failure, including
    /// the request's correlation, trace, and ASP.NET Core request identifiers.
    /// </summary>
    private void LogValidationException(HttpContext httpContext, Exception exception, int statusCode)
    {
        logger.LogWarning(
            exception,
            "Validation failed on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}. TraceId: {TraceId}. RequestId: {RequestId}. ExceptionType: {ExceptionType}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            RequestDiagnosticsContext.GetCorrelationId(httpContext),
            RequestDiagnosticsContext.GetTraceId(),
            httpContext.TraceIdentifier,
            exception.GetType().Name);
    }

    /// <summary>
    /// Logs business exceptions as warnings and unexpected exceptions as errors,
    /// together with the request diagnostic identifiers.
    /// </summary>
    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        if (exception is BusinessException)
        {
            logger.LogWarning(
                exception,
                "Business error on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}. TraceId: {TraceId}. RequestId: {RequestId}. ExceptionType: {ExceptionType}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                RequestDiagnosticsContext.GetCorrelationId(httpContext),
                RequestDiagnosticsContext.GetTraceId(),
                httpContext.TraceIdentifier,
                exception.GetType().Name);

            return;
        }

        logger.LogError(
            exception,
            "Unhandled exception on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}. TraceId: {TraceId}. RequestId: {RequestId}. ExceptionType: {ExceptionType}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            RequestDiagnosticsContext.GetCorrelationId(httpContext),
            RequestDiagnosticsContext.GetTraceId(),
            httpContext.TraceIdentifier,
            exception.GetType().Name);
    }

    /// <summary>
    /// Copies the current request's correlation ID and active OpenTelemetry trace
    /// ID into a Problem Details response when available.
    /// </summary>
    /// <param name="problemDetails">The error response to enrich.</param>
    /// <param name="httpContext">The current HTTP request context.</param>
    private static void AddDiagnosticIdentifiers(ProblemDetails problemDetails, HttpContext httpContext)
    {
        if (RequestDiagnosticsContext.GetCorrelationId(httpContext) is { } correlationId)
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        if (RequestDiagnosticsContext.GetTraceId() is { } traceId)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
    }
}
