using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public readonly struct ProviderHotelId
{
    public long Value { get; }

    public ProviderHotelId(long value)
    {
        if (value <= 0)
            throw new DomainException("Invalid provider hotel id.");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
