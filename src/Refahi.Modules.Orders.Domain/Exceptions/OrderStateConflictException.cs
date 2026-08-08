namespace Refahi.Modules.Orders.Domain.Exceptions;

public sealed class OrderStateConflictException : Exception
{
    public OrderStateConflictException(string message) : base(message)
    {
    }
}
