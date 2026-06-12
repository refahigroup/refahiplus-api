using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class FareBreakdown : ValueObject
{
    private FareBreakdown()
    {
        BaseFare = Money.Zero();
        Taxes = Money.Zero();
        Fees = Money.Zero();
        Discount = Money.Zero();
        PayableAmount = Money.Zero();
    }

    public FareBreakdown(
        Money baseFare,
        Money taxes,
        Money fees,
        Money discount,
        Money payableAmount)
    {
        EnsureSameCurrency(baseFare, taxes, fees, discount, payableAmount);

        var calculatedPayable = baseFare.Amount + taxes.Amount + fees.Amount - discount.Amount;
        if (calculatedPayable < 0)
        {
            throw new DomainException("Fare payable amount cannot be negative.");
        }

        if (calculatedPayable != payableAmount.Amount)
        {
            throw new DomainException("Fare payable amount does not match breakdown.");
        }

        BaseFare = baseFare;
        Taxes = taxes;
        Fees = fees;
        Discount = discount;
        PayableAmount = payableAmount;
    }

    public Money BaseFare { get; private set; }
    public Money Taxes { get; private set; }
    public Money Fees { get; private set; }
    public Money Discount { get; private set; }
    public Money PayableAmount { get; private set; }

    private static void EnsureSameCurrency(params Money[] values)
    {
        if (values.Length == 0)
        {
            return;
        }

        var currency = values[0].Currency;
        if (values.Any(value => !string.Equals(currency, value.Currency, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Fare breakdown currencies must match.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BaseFare;
        yield return Taxes;
        yield return Fees;
        yield return Discount;
        yield return PayableAmount;
    }
}
