namespace Refahi.Modules.Orders.Domain.Exceptions;

public class OrderDomainException : Exception
{
    public string ErrorCode { get; }

    public OrderDomainException(string message, string errorCode = "ORDER_DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
