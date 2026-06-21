using System.Net;

namespace MoneyTransferService.Business.Exceptions;

public class InvalidCustomerRequestException : BusinessException
{
    public InvalidCustomerRequestException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class CustomerCreationException : BusinessException
{
    public CustomerCreationException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}