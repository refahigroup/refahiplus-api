using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class SnappTripHotelMoneyTests
{
    [Fact]
    public void SearchMapper_UsesOriginalPriceAndConvertsItToRials()
    {
        var response = new SnappTripAvailabilityResponse
        {
            hotel_id = 10,
            availability =
            [
                new SnappTripRoomAvailability
                {
                    pricing = new SnappTripRoomPricing
                    {
                        original_sell_price = 1_200_000,
                        price = 900_000,
                        discount_amount = 300_000,
                    },
                },
            ],
        };

        var result = Assert.Single(SnappTripMapper.MapSearchResults(response));

        Assert.Equal(12_000_000, result.MinCustomerPrice);
    }

    [Fact]
    public void BookingMapper_ConvertsProviderTomansToRials()
    {
        var result = SnappTripMapper.MapCreateBooking(
            new SnappTripBookingCreateResponse
            {
                reservation_code = "booking-1",
                price = 1_200_000,
            }
        );

        Assert.Equal(12_000_000, result.Price);
        Assert.Equal("IRR", result.Currency);
    }

    [Fact]
    public void ToRials_ConvertsProviderTomansExactlyOnce()
    {
        Assert.Equal(12_345_670, SnappTripHotelMoney.ToRials(1_234_567, "اتاق"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void ToRials_RejectsMissingNegativeAndOverflowingPrices(long value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SnappTripHotelMoney.ToRials(value, "اتاق")
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void TryToRials_DropsInvalidOffers(long value)
    {
        Assert.False(SnappTripHotelMoney.TryToRials(value, out var result));
        Assert.Equal(0, result);
    }

    [Fact]
    public void ToProviderTomans_ConvertsRialFilterExactlyOnce()
    {
        Assert.Equal(1_234_567, SnappTripHotelMoney.ToProviderTomans(12_345_670, "قیمت"));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(11)]
    [InlineData(21_474_836_480)]
    public void ToProviderTomans_RejectsInvalidRialFilters(long value)
    {
        Assert.ThrowsAny<Exception>(() =>
            SnappTripHotelMoney.ToProviderTomans(value, "قیمت")
        );
    }
}
