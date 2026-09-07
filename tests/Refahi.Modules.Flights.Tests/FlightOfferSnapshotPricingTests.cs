using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;
using Refahi.Modules.Flights.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightOfferSnapshotPricingTests
{
    [Fact]
    public void Create_AcceptsZeroCommissionAndPersistsPricingVersionTwo()
    {
        var snapshot = Create(1_200_000, 0, 1_200_000, "IRR");

        Assert.Equal(1_200_000, snapshot.TotalFareAmount);
        Assert.Equal(0, snapshot.CommissionAmount);
        Assert.Equal(1_200_000, snapshot.CustomerPayableAmount);
        Assert.Equal(FlightOfferSnapshot.CurrentPricingVersion, snapshot.PricingVersion);
    }

    [Fact]
    public void Create_RejectsNonIrrCurrency()
    {
        Assert.Throws<DomainException>(() => Create(1_200_000, 0, 1_200_000, "USD"));
    }

    [Fact]
    public void Create_RejectsOverflowingComponents()
    {
        Assert.Throws<DomainException>(() => Create(long.MaxValue, 1, long.MaxValue, "IRR"));
    }

    private static FlightOfferSnapshot Create(
        long totalFare,
        long commission,
        long payable,
        string currency
    ) =>
        FlightOfferSnapshot.Create(
            "token",
            "SnappTrip",
            "fare",
            "search",
            "trace",
            totalFare,
            commission,
            payable,
            currency,
            "{}",
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(20)
        );
}
