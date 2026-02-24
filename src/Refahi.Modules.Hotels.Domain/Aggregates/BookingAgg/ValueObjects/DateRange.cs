using Refahi.Modules.Hotels.Domain.Abstraction;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public class DateRange : ValueObject
{
    public DateOnly CheckIn { get; private set; }
    public DateOnly CheckOut { get; private set; }

    private DateRange() { } // EF Core

    public DateRange(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkIn >= checkOut)
            throw new DomainException("Check-in must be before check-out.");

        CheckIn = checkIn;
        CheckOut = checkOut;
    }

    public int Nights =>
        (CheckOut.ToDateTime(TimeOnly.MinValue) - CheckIn.ToDateTime(TimeOnly.MinValue)).Days;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CheckIn;
        yield return CheckOut;
    }
}
