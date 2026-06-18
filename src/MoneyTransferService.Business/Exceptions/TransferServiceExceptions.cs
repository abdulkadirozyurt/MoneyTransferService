using System;

namespace MoneyTransferService.Business.Exceptions;

public class InvalidTransferAmountException : Exception
{
    public InvalidTransferAmountException(string message) : base(message) { }
}

public class SameAccountTransferException : Exception
{
    public SameAccountTransferException(string message) : base(message) { }
}

public class AccountNotFoundException : Exception
{
    public AccountNotFoundException(string message) : base(message) { }
}

public class AccountNotActiveException : Exception
{
    public AccountNotActiveException(string message) : base(message) { }
}

public class CurrencyMismatchException : Exception
{
    public CurrencyMismatchException(string message) : base(message) { }
}

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception? innerException = null) : base(message, innerException) { }
}
