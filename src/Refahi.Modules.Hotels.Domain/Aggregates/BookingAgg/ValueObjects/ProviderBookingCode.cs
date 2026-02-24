using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public readonly struct ProviderBookingCode
{
    public string Value { get; }

    public ProviderBookingCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Provider booking code is required.");

        Value = value;
    }

    public override string ToString() => Value;
}