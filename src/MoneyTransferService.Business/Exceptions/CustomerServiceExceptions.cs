namespace MoneyTransferService.Business.Exceptions;

public class InvalidCustomerRequestException : Exception
{
    public InvalidCustomerRequestException(string message) : base(message) { }
}

public class CustomerCreationException : Exception
{
    public CustomerCreationException(string message, Exception? innerException = null) : base(message, innerException) { }
}