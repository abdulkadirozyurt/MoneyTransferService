using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MoneyTransferService.Business.Exceptions;

namespace MoneyTransferService.WebAPI.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred while processing the request. Trace ID: {TraceId}", httpContext.TraceIdentifier);
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

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
