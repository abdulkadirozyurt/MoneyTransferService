using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MoneyTransferService.Business.Exceptions;

namespace MoneyTransferService.WebAPI.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
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

            validationProblemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            httpContext.Response.StatusCode = validationStatus;

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

        LogException(httpContext, exception, status);

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

    private void LogValidationException(HttpContext httpContext, Exception exception, int statusCode)
    {
        logger.LogWarning(
            exception,
            "Validation failed on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. TraceId: {TraceId}. ExceptionType: {ExceptionType}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            httpContext.TraceIdentifier,
            exception.GetType().Name);
    }

    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        if (exception is BusinessException)
        {
            logger.LogWarning(
                exception,
                "Business error on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. TraceId: {TraceId}. ExceptionType: {ExceptionType}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                httpContext.TraceIdentifier,
                exception.GetType().Name);

            return;
        }

        logger.LogError(
            exception,
            "Unhandled exception on {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. TraceId: {TraceId}. ExceptionType: {ExceptionType}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            httpContext.TraceIdentifier,
            exception.GetType().Name);
    }
}
