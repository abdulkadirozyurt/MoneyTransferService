using System.Net;

namespace MoneyTransferService.Business.Exceptions;

/// <summary>
/// Indicates that a transfer amount is invalid for the requested operation.
/// </summary>
public class InvalidTransferAmountException : BusinessException
{
    public InvalidTransferAmountException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

/// <summary>
/// Indicates that sender and receiver accounts are the same account.
/// </summary>
public class SameAccountTransferException : BusinessException
{
    public SameAccountTransferException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

/// <summary>
/// Indicates that a required account could not be found.
/// </summary>
public class AccountNotFoundException : BusinessException
{
    public AccountNotFoundException(string message) : base(HttpStatusCode.NotFound, message) { }
}

/// <summary>
/// Indicates that an account exists but cannot be used because it is not active.
/// </summary>
public class AccountNotActiveException : BusinessException
{
    public AccountNotActiveException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

/// <summary>
/// Indicates that account currency does not match requested transfer currency.
/// </summary>
public class CurrencyMismatchException : BusinessException
{
    public CurrencyMismatchException(string message) : base(HttpStatusCode.BadRequest, message) { }
}

/// <summary>
/// Indicates that sender balance is not enough to complete the transfer.
/// </summary>
public class InsufficientFundsException : BusinessException
{
    public InsufficientFundsException(string message) : base(HttpStatusCode.Conflict, message) { }
}

/// <summary>
/// Indicates that transfer could not complete because concurrent account updates kept conflicting.
/// </summary>
public class ConcurrencyException : BusinessException
{
    public ConcurrencyException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}

/// <summary>
/// Indicates that transfer state could not be persisted after database save failed.
/// </summary>
public class TransferPersistenceException : BusinessException
{
    public TransferPersistenceException(string message, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, message, innerException)
    {
    }
}
