using System.Net;

namespace MoneyTransferService.Business.Exceptions;

/// <summary>
/// Represents a business or domain failure that can be safely mapped to an HTTP response.
/// </summary>
public abstract class BusinessException(
    HttpStatusCode statusCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the HTTP status code that should be returned for this business failure.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}