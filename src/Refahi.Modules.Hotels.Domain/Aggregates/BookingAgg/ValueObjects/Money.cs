using Refahi.Modules.Hotels.Domain.Abstraction;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public class Money : ValueObject
{
    public long Amount { get; private set; }
    public string Currency { get; private set; }

    private Money() { } // EF

    public Money(long amount, string currency = "IRT")
    {
        if (amount < 0)
            throw new DomainException("Money amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency is required.");

        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
