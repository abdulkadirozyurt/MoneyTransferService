namespace MoneyTransferService.Business.Exceptions;

public class InvalidAccountRequestException : Exception
{
    public InvalidAccountRequestException(string message) : base(message) { }
}

public class AccountOwnerNotFoundException : Exception
{
    public AccountOwnerNotFoundException(string message) : base(message) { }
}

public class AccountCreationException : Exception
{
    public AccountCreationException(string message, Exception? innerException = null) : base(message, innerException) { }
}
