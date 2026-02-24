using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public readonly struct ProviderRoomId
{
    public long Value { get; }

    public ProviderRoomId(long value)
    {
        if (value <= 0)
            throw new DomainException("Invalid provider room id.");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}

