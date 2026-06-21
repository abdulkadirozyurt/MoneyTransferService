using System.Net;

namespace MoneyTransferService.Business.Exceptions;

public abstract class BusinessException(
    HttpStatusCode statusCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}