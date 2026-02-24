namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
