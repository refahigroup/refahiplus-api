using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Application.Services.Airlines;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Bookings;

internal static class FlightBookingDtoMapper
{
    public static FlightBookingDetailDto ToDetailDto(
        FlightBooking booking,
        IReadOnlyDictionary<string, AirlinePresentation>? airlinePresentations = null,
        IReadOnlyDictionary<string, FlightAirportPresentation>? airportPresentations = null
    )
    {
        return new FlightBookingDetailDto(
            booking.Id.Value,
            booking.UserId,
            booking.Status.ToString(),
            booking.FareBreakdown.PayableAmount.Amount,
            booking.FareBreakdown.PayableAmount.Currency,
            booking.OrderId,
            booking.OrderNumber,
            booking.Provider.ProviderName,
            booking.Provider.ProviderCaption,
            booking.Provider.ProviderTraceId ?? booking.ProviderBooking?.ProviderTraceId,
            booking.SelectedFare.ProviderFareId,
            booking.ProviderBooking?.ProviderBookingId,
            booking.ProviderBooking?.ProviderBookingCaption,
            booking.ExpiresAtUtc,
            booking.IssueFailureReason,
            booking
                .Passengers.Select(passenger => new FlightBookingPassengerDto(
                    passenger.Id,
                    passenger.FirstName,
                    passenger.LastName,
                    passenger.Type.ToString(),
                    passenger.BirthDate,
                    passenger.NationalCode,
                    passenger.PassportNumber,
                    passenger.NationalityCode
                ))
                .ToList(),
            booking
                .Segments.OrderBy(segment => segment.Sequence)
                .Select(segment => new FlightBookingSegmentDto(
                    segment.Sequence,
                    segment.FlightNumber,
                    segment.AirlineCode,
                    AirlineName(segment.AirlineCode, segment.AirlineName, airlinePresentations),
                    segment.OriginAirportCode,
                    AirportCaption(segment.OriginAirportCode, segment.OriginCaption, airportPresentations),
                    segment.DestinationAirportCode,
                    AirportCaption(segment.DestinationAirportCode, segment.DestinationCaption, airportPresentations),
                    segment.DepartureAtUtc,
                    segment.ArrivalAtUtc,
                    airlinePresentations
                        ?.GetValueOrDefault(segment.AirlineCode.Trim().ToUpperInvariant())
                        ?.LogoUrl
                ))
                .ToList(),
            ToTicketDtos(booking)
        );
    }

    private static string AirlineName(
        string code,
        string storedName,
        IReadOnlyDictionary<string, AirlinePresentation>? presentations
    )
    {
        if (
            !string.IsNullOrWhiteSpace(storedName)
            && !string.Equals(storedName.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase)
        )
            return storedName.Trim();

        return presentations?.GetValueOrDefault(code.Trim().ToUpperInvariant())?.Name
            ?? "شرکت هواپیمایی نامشخص";
    }

    private static string AirportCaption(
        string code,
        string storedCaption,
        IReadOnlyDictionary<string, FlightAirportPresentation>? presentations
    )
    {
        if (presentations?.TryGetValue(code.Trim(), out var presentation) == true)
            return string.Equals(
                presentation.CityNameFa,
                presentation.AirportNameFa,
                StringComparison.OrdinalIgnoreCase
            )
                ? presentation.CityNameFa
                : $"{presentation.CityNameFa}، {presentation.AirportNameFa}";

        return !string.IsNullOrWhiteSpace(storedCaption)
               && !string.Equals(storedCaption.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase)
            ? storedCaption.Trim()
            : "شهر نامشخص";
    }

    public static IReadOnlyCollection<FlightIssuedTicketDto> ToTicketDtos(FlightBooking booking)
    {
        return booking
            .IssuedTickets.Select(ticket => new FlightIssuedTicketDto(
                ticket.Id,
                ticket.PassengerId,
                ticket.TicketNumber,
                ticket.PassengerNameSnapshot,
                ticket.ProviderTicketId,
                ticket.IssuedAtUtc
            ))
            .ToList();
    }
}
