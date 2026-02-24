
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public readonly struct BookingId
{
    public Guid Value { get; }

    public BookingId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("BookingId cannot be empty.");

        Value = value;
    }

    public static BookingId New() => new BookingId(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
