using System.Net;

namespace MoneyTransferService.Business.Exceptions;

public class InvalidTransferAmountException : BusinessException
{
    public InvalidTransferAmountException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class SameAccountTransferException : BusinessException
{
    public SameAccountTransferException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class AccountNotFoundException : BusinessException
{
    public AccountNotFoundException(string message) : base(HttpStatusCode.NotFound, message) { }
}

public class AccountNotActiveException : BusinessException
{
    public AccountNotActiveException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class CurrencyMismatchException : BusinessException
{
    public CurrencyMismatchException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

public class InsufficientFundsException : BusinessException
{
    public InsufficientFundsException(string message) : base(HttpStatusCode.Conflict, message) { }
}

public class ConcurrencyException : BusinessException
{
    public ConcurrencyException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}

public class TransferPersistenceException : BusinessException
{
    public TransferPersistenceException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}
