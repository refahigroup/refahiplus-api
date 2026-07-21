using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class SnappTripFlightMapperTests
{
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
                new FlightSearchLeg(
                    new DateOnly(2026, 7, 15),
                    "DXB",
                    "LHR",
                    "Airport",
                    "Airport")
            ],
            new FlightTravelPreference("Economy", "OneWay", null));

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
                new FlightSearchLeg(
                    new DateOnly(2026, 7, 15),
                    "THR",
                    "MHD",
                    "Airport",
                    "Airport")
            ],
            new FlightTravelPreference(
                CabinType: "Economy",
                AirTripType: "OneWay",
                MaxStopsQuantity: 0,
                VendorExcludeCodes: ["XX"],
                VendorPreferenceCodes: ["IR"]));

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
                new FlightSearchLeg(
                    new DateOnly(2026, 7, 15),
                    "THR",
                    "MHD",
                    "AIRPORT",
                    "AIRPORT")
            ],
            new FlightTravelPreference(
                CabinType: "ECONOMY",
                AirTripType: "ONEWAY",
                MaxStopsQuantity: null));

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
                new FlightSearchLeg(
                    new DateOnly(2026, 7, 15),
                    "THR",
                    "MHD",
                    "Airport",
                    "Airport"),
                new FlightSearchLeg(
                    new DateOnly(2026, 7, 20),
                    "MHD",
                    "THR",
                    "Airport",
                    "Airport")
            ],
            new FlightTravelPreference(
                CabinType: "Economy",
                AirTripType: "RoundTrip",
                MaxStopsQuantity: null));

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
                new SnappTripPricedItinerary
                {
                    FareSourceCode = "",
                    OriginDestinationOptions = []
                },
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
                            BaseFare = 1_000,
                            TotalFare = 1_200,
                            TotalTax = 200,
                            Currency = "IRR"
                        },
                        PtcFareBreakdown =
                        [
                            new SnappTripPtcFareBreakdown
                            {
                                PassengerTypeQuantity = new SnappTripPassengerTypeQuantity
                                {
                                    PassengerType = "Adult",
                                    Quantity = 1
                                },
                                PassengerFare = new SnappTripPassengerFare
                                {
                                    BaseFare = 1_000,
                                    TotalFare = 1_200,
                                    Currency = "IRR"
                                }
                            }
                        ]
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
                                    FlightNumber = "1234"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var mapped = SnappTripFlightMapper.ToFlightResponse(response, maskedRawPayload: "{\"masked\":true}");

        var offer = Assert.Single(mapped.Offers);
        Assert.Equal("SnappTrip", mapped.ProviderName);
        Assert.Equal("123", mapped.SearchId);
        Assert.Equal("fare-1", offer.ProviderFareSourceCode);
        Assert.Equal(1_200, offer.TotalFare.TotalFare);
        Assert.Equal("{\"masked\":true}", mapped.RawPayloadSnapshot);
        Assert.Equal("{\"masked\":true}", offer.RawPayloadSnapshot);
    }
}
