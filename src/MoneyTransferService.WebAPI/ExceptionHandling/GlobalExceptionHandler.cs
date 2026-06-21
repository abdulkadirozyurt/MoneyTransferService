using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MoneyTransferService.Business.Exceptions;

namespace MoneyTransferService.WebAPI.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred while processing the request. Trace ID: {TraceId}", httpContext.TraceIdentifier);

        if (exception is ValidationException validationException)
        {
            var validationProblemDetails = new HttpValidationProblemDetails(
                validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Detail = "Please refer to the errors property for additional details.",
                Instance = httpContext.Request.Path
            };

            validationProblemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            await httpContext.Response.WriteAsJsonAsync(validationProblemDetails, cancellationToken);

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
