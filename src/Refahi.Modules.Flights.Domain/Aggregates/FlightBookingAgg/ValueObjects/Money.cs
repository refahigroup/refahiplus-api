using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class Money : ValueObject
{
    public const string DefaultCurrency = "IRR";

    private Money()
    {
        Currency = DefaultCurrency;
    }

    public Money(long amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            throw new DomainException("Money amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("Currency is required.");
        }

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    public long Amount { get; private set; }
    public string Currency { get; private set; }

    public static Money Zero(string currency = DefaultCurrency) => new(0, currency);

    public void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Currency mismatch.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
