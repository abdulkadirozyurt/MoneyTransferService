using System.Net;

namespace MoneyTransferService.Business.Exceptions;

public class InvalidAccountRequestException : BusinessException
{
    public InvalidAccountRequestException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class AccountOwnerNotFoundException : BusinessException
{
    public AccountOwnerNotFoundException(string message) : base(HttpStatusCode.NotFound, message) { }
}

public class AccountCreationException : BusinessException
{
    public AccountCreationException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}
