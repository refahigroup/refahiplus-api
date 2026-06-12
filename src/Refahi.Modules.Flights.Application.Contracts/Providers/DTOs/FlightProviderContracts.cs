namespace Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;

public sealed record FlightSearchRequest(
    int Adult,
    int Child,
    int Infant,
    bool? IsDomestic,
    IReadOnlyCollection<FlightSearchLeg> OriginDestinationInformations,
    FlightTravelPreference? TravelPreference);

public sealed record FlightSearchLeg(
    DateOnly DepartureDate,
    string OriginLocationCode,
    string DestinationLocationCode,
    string OriginType,
    string DestinationType,
    string? OriginCaption = null,
    string? DestinationCaption = null);

public sealed record FlightTravelPreference(
    string CabinType,
    string AirTripType,
    int? MaxStopsQuantity = null,
    IReadOnlyCollection<string>? VendorExcludeCodes = null,
    IReadOnlyCollection<string>? VendorPreferenceCodes = null);

public sealed record FlightSearchResponse(
    bool Success,
    string ProviderName,
    string? SearchId,
    IReadOnlyCollection<FlightFareOffer> Offers,
    FlightProviderError? Error = null,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightFareOffer(
    string ProviderName,
    string? SearchId,
    string ProviderFareSourceCode,
    string? Direction,
    string? ValidatingAirlineCode,
    string? ValidatingAirlineCaption,
    FlightMoney TotalFare,
    IReadOnlyCollection<FlightOriginDestinationOption> OriginDestinationOptions,
    IReadOnlyCollection<FlightPassengerFareBreakdown> PassengerFareBreakdowns,
    string? FareType,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightOriginDestinationOption(
    IReadOnlyCollection<FlightSegmentOffer> FlightSegments,
    int? JourneyDurationPerMinute,
    int? ConnectionTimePerMinute);

public sealed record FlightSegmentOffer(
    string? ProviderSegmentId,
    string DepartureAirportLocationCode,
    string? DepartureAirportCaption,
    string ArrivalAirportLocationCode,
    string? ArrivalAirportCaption,
    DateTime? DepartureDateTime,
    DateTime? ArrivalDateTime,
    string? FlightNumber,
    string? MarketingAirlineCode,
    string? MarketingAirlineCaption,
    string? OperatingAirlineCode,
    string? OperatingAirlineCaption,
    string? OperatingAirlineFlightNumber,
    string? Equipment,
    string? CabinClassCode,
    string? CabinClassCaption,
    string? ResBookDesigCode,
    int? JourneyDurationPerMinute,
    int? ConnectionTimePerMinute,
    int? SeatsRemaining,
    int? StopQuantity,
    string? Baggage,
    bool? IsCharter,
    bool? IsReturn);

public sealed record FlightPassengerFareBreakdown(
    string? PassengerType,
    int Quantity,
    FlightMoney Fare);

public sealed record FlightMoney(
    long BaseFare,
    long TotalFare,
    long TotalTax,
    long TotalCommission,
    long ServiceTax,
    string Currency);

public sealed record FlightBookRequest(
    string ProviderFareSourceCode,
    string PhoneNumber,
    string Email,
    IReadOnlyCollection<FlightBookPassenger> Passengers);

public sealed record FlightBookPassenger(
    string NationalityCode,
    string? NationalId,
    string FirstName,
    string LastName,
    string Gender,
    DateOnly Birthday,
    string PassengerType,
    FlightPassportInfo? PassportInfo);

public sealed record FlightPassportInfo(
    string? CountryCode,
    DateOnly? IssueDate,
    DateOnly? ExpireDate,
    string? Number);

public sealed record FlightBookResponse(
    string? TrackingCode,
    string? CheckoutUrl,
    string? BookId,
    string? PaymentCurrency,
    long? PaymentAmount,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightIssueRequest(string BookId);

public sealed record FlightIssueResponse(
    string? Status,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightInquiryRequest(string BookId);

public sealed record FlightInquiryResponse(
    string? Status,
    string? TrackingCode,
    string? Pnr,
    FlightInquiryBuyer? Buyer,
    IReadOnlyCollection<FlightInquiryTicket> Tickets,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightInquiryBuyer(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Email,
    string? ReceiverPhoneNumber,
    string? ReceiverEmail);

public sealed record FlightInquiryTicket(
    string? Serial,
    string? Pnr,
    string? PassengerName,
    string? PassengerType,
    string? Direction,
    string? DocumentType,
    string? DocumentId,
    IReadOnlyCollection<FlightCancellationRoute> CancellationRoutes);

public sealed record FlightCancellationRoute(
    string RouteId,
    string? Direction,
    string? Origin,
    string? OriginCaption,
    string? Destination,
    string? DestinationCaption);

public sealed record FlightCancellationQuoteRequest(
    string TrackingCode,
    string RouteId,
    IReadOnlyCollection<string> TicketSerials,
    int ReasonId);

public sealed record FlightCancellationQuoteResponse(
    string? PenaltyStatus,
    bool IsEligibleForCancellation,
    bool IsValid,
    string RouteId,
    string? Currency,
    long TotalAmount,
    long TotalPenaltyAmount,
    long TotalRefundAmount,
    IReadOnlyCollection<FlightCancellationTicketQuote> Tickets,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightCancellationTicketQuote(
    string TicketSerial,
    string? PassengerName,
    string? PassengerType,
    int? PenaltyPercent,
    long PenaltyAmount,
    long RefundAmount,
    long TicketPrice,
    string? Currency,
    bool NoShow,
    bool IsValid);

public sealed record FlightCancellationSubmitRequest(
    string TrackingCode,
    string? Phone,
    string RouteId,
    IReadOnlyCollection<string> TicketSerials,
    int ReasonId);

public sealed record FlightCancellationSubmitResponse(
    string? Message,
    string? Status,
    string? ProviderCancellationId,
    string? ProviderTraceId = null,
    string? RawPayloadSnapshot = null);

public sealed record FlightProviderError(string? Code, string? Message);
