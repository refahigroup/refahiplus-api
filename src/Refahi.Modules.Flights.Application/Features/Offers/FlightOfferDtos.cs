namespace Refahi.Modules.Flights.Application.Features.Offers;

public sealed record FlightMoneyDto(
    long BaseFare,
    long TotalFare,
    long TotalTax,
    long TotalCommission,
    long ServiceTax,
    string Currency);

public sealed record FlightPassengerFareDto(
    string? PassengerType,
    int Quantity,
    FlightMoneyDto Fare);

public sealed record FlightSegmentDto(
    string DepartureAirportCode,
    string? DepartureAirportCaption,
    string ArrivalAirportCode,
    string? ArrivalAirportCaption,
    DateTime? DepartureDateTime,
    DateTime? ArrivalDateTime,
    string? FlightNumber,
    string? MarketingAirlineCode,
    string? MarketingAirlineCaption,
    string? OperatingAirlineCode,
    string? OperatingAirlineCaption,
    string? CabinClassCode,
    string? CabinClassCaption,
    string? BookingClass,
    int? DurationMinutes,
    int? SeatsRemaining,
    int? StopQuantity,
    string? Baggage,
    bool? IsCharter,
    bool? IsReturn);

public sealed record FlightOfferDto(
    string OfferToken,
    DateTime ExpiresAtUtc,
    string? Direction,
    string OriginAirportCode,
    string? OriginAirportCaption,
    string DestinationAirportCode,
    string? DestinationAirportCaption,
    DateTime? DepartureDateTime,
    DateTime? ArrivalDateTime,
    string? AirlineCode,
    string? AirlineCaption,
    string? FlightNumber,
    string? CabinClassCode,
    string? CabinClassCaption,
    string? FareType,
    int? DurationMinutes,
    int StopCount,
    int? SeatsRemaining,
    string? Baggage,
    FlightMoneyDto TotalFare,
    IReadOnlyCollection<FlightSegmentDto> Segments,
    IReadOnlyCollection<FlightPassengerFareDto> PassengerFares);
