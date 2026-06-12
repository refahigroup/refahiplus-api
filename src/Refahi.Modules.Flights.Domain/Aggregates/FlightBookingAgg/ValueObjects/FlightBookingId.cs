using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public readonly struct FlightBookingId
{
    public Guid Value { get; }

    public FlightBookingId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("Flight booking id cannot be empty.");
        }

        Value = value;
    }

    public static FlightBookingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
