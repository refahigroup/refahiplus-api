using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class SnappTripFlightMapperTests
{
    [Fact]
    public void ToSnappTripRequest_NormalizesBookingEnumsAndOptionalValues()
    {
        var request = new FlightBookRequest(
            "fare-1",
            "+989121234567",
            "passenger@example.com",
            [
                new FlightBookPassenger(
                    " ir ",
                    " 0154721621 ",
                    " Ali ",
                    " Karimi ",
                    "Male",
                    new DateOnly(1990, 4, 1),
                    "Adult",
                    new FlightPassportInfo(" ir ", null, null, " ")
                ),
            ]
        );

        var passenger = Assert.Single(SnappTripFlightMapper.ToSnappTripRequest(request).Passengers);

        Assert.Equal("IR", passenger.NationalityCode);
        Assert.Equal("0154721621", passenger.NationalId);
        Assert.Equal("Ali", passenger.FirstName);
        Assert.Equal("Karimi", passenger.LastName);
        Assert.Equal("MALE", passenger.Gender);
        Assert.Equal("ADULT", passenger.PassengerType);
        Assert.Equal("IR", passenger.PassportInfo?.CountryCode);
        Assert.Null(passenger.PassportInfo?.Number);
    }

    [Fact]
    public void ToSnappTripRequest_PreservesPassengerUpperBoundary()
    {
        var request = new FlightSearchRequest(
            Adult: 20,
            Child: 20,
            Infant: 20,
            IsDomestic: false,
            OriginDestinationInformations:
            [
                new FlightSearchLeg(new DateOnly(2026, 7, 15), "DXB", "LHR", "Airport", "Airport"),
            ],
            new FlightTravelPreference("Economy", "OneWay", null)
        );

        var mapped = SnappTripFlightMapper.ToSnappTripRequest(request);

        Assert.Equal(20, mapped.Adult);
        Assert.Equal(20, mapped.Child);
        Assert.Equal(20, mapped.Infant);
    }

    [Fact]
    public void ToSnappTripRequest_MapsSearchLegsAndTravelPreference()
    {
        var request = new FlightSearchRequest(
            Adult: 1,
            Child: 1,
            Infant: 0,
            IsDomestic: true,
            OriginDestinationInformations:
            [
                new FlightSearchLeg(new DateOnly(2026, 7, 15), "THR", "MHD", "Airport", "Airport"),
            ],
            new FlightTravelPreference(
                CabinType: "Economy",
                AirTripType: "OneWay",
                MaxStopsQuantity: 0,
                VendorExcludeCodes: ["XX"],
                VendorPreferenceCodes: ["IR"]
            )
        );

        var mapped = SnappTripFlightMapper.ToSnappTripRequest(request);

        Assert.Equal(1, mapped.Adult);
        Assert.True(mapped.IsDomestic);
        Assert.Equal("2026-07-15", mapped.OriginDestinationInformations.Single().DepartureDate);
        Assert.Equal("THR", mapped.OriginDestinationInformations.Single().OriginLocationCode);
        Assert.Equal("MHD", mapped.OriginDestinationInformations.Single().DestinationLocationCode);
        Assert.Equal("AIRPORT", mapped.OriginDestinationInformations.Single().OriginType);
        Assert.Equal("AIRPORT", mapped.OriginDestinationInformations.Single().DestinationType);
        Assert.Equal("ECONOMY", mapped.TravelPreference!.CabinType);
        Assert.Equal("ONEWAY", mapped.TravelPreference.AirTripType);
        Assert.Equal("0", mapped.TravelPreference.MaxStopsQuantity);
        Assert.Equal(["XX"], mapped.TravelPreference.VendorExcludeCodes);
        Assert.Equal(["IR"], mapped.TravelPreference.VendorPreferenceCodes);
    }

    [Fact]
    public void ToSnappTripRequest_MapsMissingMaxStopsToAll()
    {
        var request = new FlightSearchRequest(
            Adult: 1,
            Child: 0,
            Infant: 0,
            IsDomestic: true,
            OriginDestinationInformations:
            [
                new FlightSearchLeg(new DateOnly(2026, 7, 15), "THR", "MHD", "AIRPORT", "AIRPORT"),
            ],
            new FlightTravelPreference(
                CabinType: "ECONOMY",
                AirTripType: "ONEWAY",
                MaxStopsQuantity: null
            )
        );

        var mapped = SnappTripFlightMapper.ToSnappTripRequest(request);

        Assert.Equal("ALL", mapped.TravelPreference!.MaxStopsQuantity);
    }

    [Fact]
    public void ToSnappTripRequest_MapsRoundTripToReturn()
    {
        var request = new FlightSearchRequest(
            Adult: 1,
            Child: 0,
            Infant: 0,
            IsDomestic: true,
            OriginDestinationInformations:
            [
                new FlightSearchLeg(new DateOnly(2026, 7, 15), "THR", "MHD", "Airport", "Airport"),
                new FlightSearchLeg(new DateOnly(2026, 7, 20), "MHD", "THR", "Airport", "Airport"),
            ],
            new FlightTravelPreference(
                CabinType: "Economy",
                AirTripType: "RoundTrip",
                MaxStopsQuantity: null
            )
        );

        var mapped = SnappTripFlightMapper.ToSnappTripRequest(request);

        Assert.Equal("RETURN", mapped.TravelPreference!.AirTripType);
    }

    [Fact]
    public void ToFlightResponse_DropsOffersWithoutFareSourceCodeAndPreservesSnapshot()
    {
        var response = new SnappTripSearchResponse
        {
            Success = true,
            SearchId = 123,
            PricedItineraries =
            [
                new SnappTripPricedItinerary { FareSourceCode = "", OriginDestinationOptions = [] },
                new SnappTripPricedItinerary
                {
                    FareSourceCode = "fare-1",
                    DirectionInd = "OneWay",
                    ValidatingAirlineCode = "IR",
                    AirItineraryPricingInfo = new SnappTripAirItineraryPricingInfo
                    {
                        FareType = "Public",
                        ItinTotalFare = new SnappTripItinTotalFare
                        {
                            BaseFare = 112_000_000,
                            TotalFare = 112_042_200,
                            TotalTax = 42_200,
                            TotalCommission = 4_287_800,
                            Currency = "IRR",
                        },
                        PtcFareBreakdown =
                        [
                            new SnappTripPtcFareBreakdown
                            {
                                PassengerTypeQuantity = new SnappTripPassengerTypeQuantity
                                {
                                    PassengerType = "Adult",
                                    Quantity = 1,
                                },
                                PassengerFare = new SnappTripPassengerFare
                                {
                                    BaseFare = 1_000,
                                    TotalFare = 1_200,
                                    Currency = "IRR",
                                },
                            },
                        ],
                    },
                    OriginDestinationOptions =
                    [
                        new SnappTripOriginDestinationOption
                        {
                            FlightSegments =
                            [
                                new SnappTripFlightSegment
                                {
                                    DepartureAirportLocationCode = "THR",
                                    ArrivalAirportLocationCode = "MHD",
                                    DepartureDateTime = "2026-07-15T08:00:00",
                                    ArrivalDateTime = "2026-07-15T09:30:00",
                                    FlightNumber = "1234",
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var mapped = SnappTripFlightMapper.ToFlightResponse(
            response,
            maskedRawPayload: "{\"masked\":true}",
            charterCommissionPercent: 5m
        );

        var offer = Assert.Single(mapped.Offers);
        Assert.Equal("SnappTrip", mapped.ProviderName);
        Assert.Equal("123", mapped.SearchId);
        Assert.Equal("fare-1", offer.ProviderFareSourceCode);
        Assert.Equal(112_042_200, offer.TotalFare.TotalFare);
        Assert.Equal(4_287_800, offer.TotalFare.TotalCommission);
        Assert.Equal(116_330_000, offer.TotalFare.CustomerPayableAmountMinor);
        Assert.Equal("{\"masked\":true}", mapped.RawPayloadSnapshot);
        Assert.Equal("{\"masked\":true}", offer.RawPayloadSnapshot);
    }

    [Fact]
    public void ToFlightResponse_UsesConfiguredCommissionForOfferWithAnyCharterSegment()
    {
        var response = SearchResponse(
            totalFare: 100,
            providerCommission: 99,
            segments:
            [
                new SnappTripFlightSegment { IsCharter = false },
                new SnappTripFlightSegment { IsCharter = true },
            ],
            passengerFares:
            [
                PassengerFare("Adult", 50, 40),
                PassengerFare("Child", 50, 30),
            ]
        );

        var mapped = SnappTripFlightMapper.ToFlightResponse(
            response,
            maskedRawPayload: "{\"totalCommission\":99}",
            charterCommissionPercent: 5m
        );

        var offer = Assert.Single(mapped.Offers);
        Assert.Equal(5, offer.TotalFare.TotalCommission);
        Assert.Equal(105, offer.TotalFare.CustomerPayableAmountMinor);
        Assert.Equal(5, offer.PassengerFareBreakdowns.Sum(item => item.Fare.TotalCommission));
        Assert.Equal(3, offer.PassengerFareBreakdowns.ElementAt(0).Fare.TotalCommission);
        Assert.Equal(53, offer.PassengerFareBreakdowns.ElementAt(0).Fare.CustomerPayableAmountMinor);
        Assert.Equal(2, offer.PassengerFareBreakdowns.ElementAt(1).Fare.TotalCommission);
        Assert.Equal(52, offer.PassengerFareBreakdowns.ElementAt(1).Fare.CustomerPayableAmountMinor);
        Assert.Equal("{\"totalCommission\":99}", offer.RawPayloadSnapshot);
    }

    [Fact]
    public void ToFlightResponse_RoundsCharterCommissionAwayFromZero()
    {
        var response = SearchResponse(
            totalFare: 10,
            providerCommission: 99,
            segments: [new SnappTripFlightSegment { IsCharter = true }],
            passengerFares: []
        );

        var offer = Assert.Single(
            SnappTripFlightMapper.ToFlightResponse(response, null, 5m).Offers
        );

        Assert.Equal(1, offer.TotalFare.TotalCommission);
        Assert.Equal(11, offer.TotalFare.CustomerPayableAmountMinor);
        Assert.Empty(offer.PassengerFareBreakdowns);
    }

    [Fact]
    public void ToFlightResponse_PreservesProviderCommissionWhenAllSegmentsAreNonCharter()
    {
        var response = SearchResponse(
            totalFare: 100,
            providerCommission: 7,
            segments:
            [
                new SnappTripFlightSegment { IsCharter = false },
                new SnappTripFlightSegment { IsCharter = null },
            ],
            passengerFares: [PassengerFare("Adult", 100, 7)]
        );

        var offer = Assert.Single(
            SnappTripFlightMapper.ToFlightResponse(response, null, 5m).Offers
        );

        Assert.Equal(7, offer.TotalFare.TotalCommission);
        Assert.Equal(107, offer.TotalFare.CustomerPayableAmountMinor);
        Assert.Equal(7, Assert.Single(offer.PassengerFareBreakdowns).Fare.TotalCommission);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(long.MaxValue, 1)]
    public void ToFlightResponse_RejectsInvalidOrOverflowingPayableAmount(
        long totalFare,
        long commission
    )
    {
        var response = new SnappTripSearchResponse
        {
            Success = true,
            PricedItineraries =
            [
                new SnappTripPricedItinerary
                {
                    FareSourceCode = "fare-1",
                    AirItineraryPricingInfo = new SnappTripAirItineraryPricingInfo
                    {
                        ItinTotalFare = new SnappTripItinTotalFare
                        {
                            TotalFare = totalFare,
                            TotalCommission = commission,
                            Currency = "IRR",
                        },
                    },
                },
            ],
        };

        Assert.Throws<InvalidOperationException>(() =>
            SnappTripFlightMapper.ToFlightResponse(
                response,
                maskedRawPayload: null,
                charterCommissionPercent: 5m
            )
        );
    }

    private static SnappTripSearchResponse SearchResponse(
        long totalFare,
        long providerCommission,
        IReadOnlyCollection<SnappTripFlightSegment> segments,
        IReadOnlyCollection<SnappTripPtcFareBreakdown> passengerFares
    ) =>
        new()
        {
            Success = true,
            PricedItineraries =
            [
                new SnappTripPricedItinerary
                {
                    FareSourceCode = "fare-1",
                    AirItineraryPricingInfo = new SnappTripAirItineraryPricingInfo
                    {
                        ItinTotalFare = new SnappTripItinTotalFare
                        {
                            TotalFare = totalFare,
                            TotalCommission = providerCommission,
                            Currency = "IRR",
                        },
                        PtcFareBreakdown = passengerFares.ToList(),
                    },
                    OriginDestinationOptions =
                    [
                        new SnappTripOriginDestinationOption
                        {
                            FlightSegments = segments.ToList(),
                        },
                    ],
                },
            ],
        };

    private static SnappTripPtcFareBreakdown PassengerFare(
        string passengerType,
        long totalFare,
        long providerCommission
    ) =>
        new()
        {
            PassengerTypeQuantity = new SnappTripPassengerTypeQuantity
            {
                PassengerType = passengerType,
                Quantity = 1,
            },
            PassengerFare = new SnappTripPassengerFare
            {
                TotalFare = totalFare,
                Commission = providerCommission,
                Currency = "IRR",
            },
        };
}
