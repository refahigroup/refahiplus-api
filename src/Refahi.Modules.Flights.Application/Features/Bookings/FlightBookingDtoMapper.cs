using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;

namespace Refahi.Modules.Flights.Application.Features.Bookings;

internal static class FlightBookingDtoMapper
{
    public static FlightBookingDetailDto ToDetailDto(FlightBooking booking)
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
            booking.Passengers
                .Select(passenger => new FlightBookingPassengerDto(
                    passenger.Id,
                    passenger.FirstName,
                    passenger.LastName,
                    passenger.Type.ToString(),
                    passenger.BirthDate,
                    passenger.NationalCode,
                    passenger.PassportNumber,
                    passenger.NationalityCode))
                .ToList(),
            booking.Segments
                .OrderBy(segment => segment.Sequence)
                .Select(segment => new FlightBookingSegmentDto(
                    segment.Sequence,
                    segment.FlightNumber,
                    segment.AirlineCode,
                    segment.AirlineName,
                    segment.OriginAirportCode,
                    segment.OriginCaption,
                    segment.DestinationAirportCode,
                    segment.DestinationCaption,
                    segment.DepartureAtUtc,
                    segment.ArrivalAtUtc))
                .ToList(),
            ToTicketDtos(booking));
    }

    public static IReadOnlyCollection<FlightIssuedTicketDto> ToTicketDtos(FlightBooking booking)
    {
        return booking.IssuedTickets
            .Select(ticket => new FlightIssuedTicketDto(
                ticket.Id,
                ticket.PassengerId,
                ticket.TicketNumber,
                ticket.PassengerNameSnapshot,
                ticket.ProviderTicketId,
                ticket.IssuedAtUtc))
            .ToList();
    }
}
