using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

namespace Refahi.Modules.Flights.Tests;

internal static class FlightBookingTestFactory
{
    public static FlightBooking CreateDraft(DateTime nowUtc)
    {
        return FlightBooking.CreateDraft(
            FlightBookingId.New(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new ProviderSnapshot("SnappTrip", "snapptrip", "اسنپ تریپ", "trace-1", "{}"),
            new SelectedFareSnapshot("fare-1", "تهران به مشهد", "Economy", "Y", "{}", "trace-1"),
            new ContactInfo("09120000000", "user@example.com"),
            [
                new Passenger(
                    "Ali",
                    "Ahmadi",
                    FlightPassengerType.Adult,
                    new DateOnly(1990, 1, 1),
                    "0012345678",
                    passportNumber: null,
                    "IR")
            ],
            [
                new FlightSegment(
                    sequence: 1,
                    providerSegmentId: "seg-1",
                    flightNumber: "1234",
                    airlineCode: "IR",
                    airlineName: "Iran Air",
                    originAirportCode: "THR",
                    originCaption: "تهران",
                    destinationAirportCode: "MHD",
                    destinationCaption: "مشهد",
                    departureAtUtc: nowUtc.AddHours(2),
                    arrivalAtUtc: nowUtc.AddHours(3))
            ],
            new FareBreakdown(
                new Money(1_000_000),
                new Money(200_000),
                Money.Zero(),
                Money.Zero(),
                new Money(1_200_000)),
            "booking-idempotency-key",
            nowUtc,
            nowUtc.AddMinutes(30));
    }
}
