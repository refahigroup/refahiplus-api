namespace Refahi.Modules.Flights.Application.Features.Bookings;

public sealed record FlightBookingPassengerDto(
    Guid PassengerId,
    string FirstName,
    string LastName,
    string PassengerType,
    DateOnly BirthDate,
    string? NationalCode,
    string? PassportNumber,
    string NationalityCode);

public sealed record FlightBookingSegmentDto(
    int Sequence,
    string FlightNumber,
    string AirlineCode,
    string AirlineName,
    string OriginAirportCode,
    string OriginCaption,
    string DestinationAirportCode,
    string DestinationCaption,
    DateTime DepartureAtUtc,
    DateTime ArrivalAtUtc);

public sealed record FlightIssuedTicketDto(
    Guid TicketId,
    Guid PassengerId,
    string TicketNumber,
    string PassengerNameSnapshot,
    string? ProviderTicketId,
    DateTime IssuedAtUtc);

public sealed record FlightBookingDetailDto(
    Guid BookingId,
    Guid UserId,
    string Status,
    long PayableAmountMinor,
    string Currency,
    Guid? OrderId,
    string? OrderNumber,
    string ProviderName,
    string ProviderCaption,
    string? ProviderTraceId,
    string ProviderFareId,
    string? ProviderBookingId,
    string? TrackingCode,
    DateTime? ExpiresAtUtc,
    string? IssueFailureReason,
    IReadOnlyCollection<FlightBookingPassengerDto> Passengers,
    IReadOnlyCollection<FlightBookingSegmentDto> Segments,
    IReadOnlyCollection<FlightIssuedTicketDto> IssuedTickets);

public sealed record PrepareFlightOrderResponse(
    Guid BookingId,
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string PaymentState);

public sealed record IssueFlightTicketResponse(
    Guid BookingId,
    Guid OrderId,
    string Status,
    IReadOnlyCollection<FlightIssuedTicketDto> IssuedTickets);
