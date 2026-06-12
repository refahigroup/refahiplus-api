using System.Text.Json.Serialization;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;

internal sealed class SnappTripSearchRequest
{
    [JsonPropertyName("adult")]
    public int Adult { get; set; }

    [JsonPropertyName("child")]
    public int Child { get; set; }

    [JsonPropertyName("infant")]
    public int Infant { get; set; }

    [JsonPropertyName("isDomestic")]
    public bool? IsDomestic { get; set; }

    [JsonPropertyName("originDestinationInformations")]
    public List<SnappTripOriginDestinationInformation> OriginDestinationInformations { get; set; } = new();

    [JsonPropertyName("travelPreference")]
    public SnappTripTravelPreference? TravelPreference { get; set; }
}

internal sealed class SnappTripOriginDestinationInformation
{
    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; set; } = string.Empty;

    [JsonPropertyName("originLocationCode")]
    public string OriginLocationCode { get; set; } = string.Empty;

    [JsonPropertyName("destinationLocationCode")]
    public string DestinationLocationCode { get; set; } = string.Empty;

    [JsonPropertyName("originType")]
    public string OriginType { get; set; } = string.Empty;

    [JsonPropertyName("destinationType")]
    public string DestinationType { get; set; } = string.Empty;
}

internal sealed class SnappTripTravelPreference
{
    [JsonPropertyName("cabinType")]
    public string CabinType { get; set; } = string.Empty;

    [JsonPropertyName("maxStopsQuantity")]
    public string MaxStopsQuantity { get; set; } = "ALL";

    [JsonPropertyName("airTripType")]
    public string AirTripType { get; set; } = string.Empty;

    [JsonPropertyName("vendorExcludeCodes")]
    public List<string>? VendorExcludeCodes { get; set; }

    [JsonPropertyName("vendorPreferenceCodes")]
    public List<string>? VendorPreferenceCodes { get; set; }
}

internal sealed class SnappTripSearchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("searchId")]
    public long? SearchId { get; set; }

    [JsonPropertyName("pricedItineraries")]
    public List<SnappTripPricedItinerary> PricedItineraries { get; set; } = new();

    [JsonPropertyName("error")]
    public SnappTripResponseError? Error { get; set; }
}

internal sealed class SnappTripPricedItinerary
{
    [JsonPropertyName("fareSourceCode")]
    public string FareSourceCode { get; set; } = string.Empty;

    [JsonPropertyName("directionInd")]
    public string? DirectionInd { get; set; }

    [JsonPropertyName("originDestinationOptions")]
    public List<SnappTripOriginDestinationOption> OriginDestinationOptions { get; set; } = new();

    [JsonPropertyName("airItineraryPricingInfo")]
    public SnappTripAirItineraryPricingInfo? AirItineraryPricingInfo { get; set; }

    [JsonPropertyName("validatingAirlineCode")]
    public string? ValidatingAirlineCode { get; set; }
}

internal sealed class SnappTripOriginDestinationOption
{
    [JsonPropertyName("flightSegments")]
    public List<SnappTripFlightSegment> FlightSegments { get; set; } = new();

    [JsonPropertyName("journeyDurationPerMinute")]
    public int? JourneyDurationPerMinute { get; set; }

    [JsonPropertyName("connectionTimePerMinute")]
    public int? ConnectionTimePerMinute { get; set; }
}

internal sealed class SnappTripFlightSegment
{
    [JsonPropertyName("departureAirportLocationCode")]
    public string? DepartureAirportLocationCode { get; set; }

    [JsonPropertyName("arrivalAirportLocationCode")]
    public string? ArrivalAirportLocationCode { get; set; }

    [JsonPropertyName("departureDateTime")]
    public string? DepartureDateTime { get; set; }

    [JsonPropertyName("arrivalDateTime")]
    public string? ArrivalDateTime { get; set; }

    [JsonPropertyName("flightNumber")]
    public string? FlightNumber { get; set; }

    [JsonPropertyName("marketingAirlineCode")]
    public string? MarketingAirlineCode { get; set; }

    [JsonPropertyName("operatingAirline")]
    public SnappTripOperatingAirline? OperatingAirline { get; set; }

    [JsonPropertyName("cabinClassCode")]
    public string? CabinClassCode { get; set; }

    [JsonPropertyName("resBookDesigCode")]
    public string? ResBookDesigCode { get; set; }

    [JsonPropertyName("journeyDurationPerMinute")]
    public int? JourneyDurationPerMinute { get; set; }

    [JsonPropertyName("connectionTimePerMinute")]
    public int? ConnectionTimePerMinute { get; set; }

    [JsonPropertyName("seatsRemaining")]
    public int? SeatsRemaining { get; set; }

    [JsonPropertyName("stopQuantity")]
    public int? StopQuantity { get; set; }

    [JsonPropertyName("baggage")]
    public string? Baggage { get; set; }

    [JsonPropertyName("isCharter")]
    public bool? IsCharter { get; set; }

    [JsonPropertyName("isReturn")]
    public bool? IsReturn { get; set; }
}

internal sealed class SnappTripOperatingAirline
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("flightNumber")]
    public string? FlightNumber { get; set; }

    [JsonPropertyName("equipment")]
    public string? Equipment { get; set; }
}

internal sealed class SnappTripAirItineraryPricingInfo
{
    [JsonPropertyName("itinTotalFare")]
    public SnappTripItinTotalFare? ItinTotalFare { get; set; }

    [JsonPropertyName("ptcFareBreakdown")]
    public List<SnappTripPtcFareBreakdown> PtcFareBreakdown { get; set; } = new();

    [JsonPropertyName("fareType")]
    public string? FareType { get; set; }
}

internal sealed class SnappTripItinTotalFare
{
    [JsonPropertyName("baseFare")]
    public long BaseFare { get; set; }

    [JsonPropertyName("totalFare")]
    public long TotalFare { get; set; }

    [JsonPropertyName("totalTax")]
    public long TotalTax { get; set; }

    [JsonPropertyName("totalCommission")]
    public long TotalCommission { get; set; }

    [JsonPropertyName("serviceTax")]
    public long ServiceTax { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

internal sealed class SnappTripPtcFareBreakdown
{
    [JsonPropertyName("passengerTypeQuantity")]
    public SnappTripPassengerTypeQuantity? PassengerTypeQuantity { get; set; }

    [JsonPropertyName("passengerFare")]
    public SnappTripPassengerFare? PassengerFare { get; set; }
}

internal sealed class SnappTripPassengerTypeQuantity
{
    [JsonPropertyName("passengerType")]
    public string? PassengerType { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

internal sealed class SnappTripPassengerFare
{
    [JsonPropertyName("baseFare")]
    public long BaseFare { get; set; }

    [JsonPropertyName("totalFare")]
    public long TotalFare { get; set; }

    [JsonPropertyName("serviceTax")]
    public long ServiceTax { get; set; }

    [JsonPropertyName("commission")]
    public long Commission { get; set; }

    [JsonPropertyName("priceCitizen")]
    public long PriceCitizen { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

internal sealed class SnappTripResponseError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

internal sealed class SnappTripBookRequest
{
    [JsonPropertyName("fareSourceCode")]
    public string FareSourceCode { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("passengers")]
    public List<SnappTripBookPassenger> Passengers { get; set; } = new();
}

internal sealed class SnappTripBookPassenger
{
    [JsonPropertyName("nationalityCode")]
    public string NationalityCode { get; set; } = string.Empty;

    [JsonPropertyName("nationalId")]
    public string? NationalId { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("birthday")]
    public string Birthday { get; set; } = string.Empty;

    [JsonPropertyName("passengerType")]
    public string PassengerType { get; set; } = string.Empty;

    [JsonPropertyName("passportInfo")]
    public SnappTripPassportInfo? PassportInfo { get; set; }
}

internal sealed class SnappTripPassportInfo
{
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("expireDate")]
    public string? ExpireDate { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }
}

internal sealed class SnappTripBookResponse
{
    [JsonPropertyName("trackingCode")]
    public string? TrackingCode { get; set; }

    [JsonPropertyName("checkoutUrl")]
    public string? CheckoutUrl { get; set; }

    [JsonPropertyName("bookId")]
    public string? BookId { get; set; }

    [JsonPropertyName("paymentCurrency")]
    public string? PaymentCurrency { get; set; }

    [JsonPropertyName("paymentAmount")]
    public long? PaymentAmount { get; set; }
}

internal sealed class SnappTripIssueRequest
{
    [JsonPropertyName("bookId")]
    public string BookId { get; set; } = string.Empty;
}

internal sealed class SnappTripIssueResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

internal sealed class SnappTripInquiryResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("trackingCode")]
    public string? TrackingCode { get; set; }

    [JsonPropertyName("buyer")]
    public SnappTripInquiryBuyer? Buyer { get; set; }

    [JsonPropertyName("tickets")]
    public List<SnappTripInquiryTicket> Tickets { get; set; } = new();

    [JsonPropertyName("pnr")]
    public string? Pnr { get; set; }
}

internal sealed class SnappTripInquiryBuyer
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("receiverPhoneNumber")]
    public string? ReceiverPhoneNumber { get; set; }

    [JsonPropertyName("receiverEmail")]
    public string? ReceiverEmail { get; set; }
}

internal sealed class SnappTripInquiryTicket
{
    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("pnr")]
    public string? Pnr { get; set; }

    [JsonPropertyName("passengerName")]
    public string? PassengerName { get; set; }

    [JsonPropertyName("passengerType")]
    public string? PassengerType { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("cancellationRoutes")]
    public List<SnappTripInquiryCancellationRoute> CancellationRoutes { get; set; } = new();
}

internal sealed class SnappTripInquiryCancellationRoute
{
    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("origin")]
    public string? Origin { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }
}

internal sealed class SnappTripPenaltyRequest
{
    [JsonPropertyName("trackingCode")]
    public string TrackingCode { get; set; } = string.Empty;

    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;

    [JsonPropertyName("ticketSerials")]
    public List<string> TicketSerials { get; set; } = new();

    [JsonPropertyName("reasonId")]
    public int ReasonId { get; set; }
}

internal sealed class SnappTripPenaltyResponse
{
    [JsonPropertyName("penaltyStatus")]
    public string? PenaltyStatus { get; set; }

    [JsonPropertyName("isEligibleForCancellation")]
    public bool IsEligibleForCancellation { get; set; }

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("totalAmount")]
    public long TotalAmount { get; set; }

    [JsonPropertyName("totalPenaltyAmount")]
    public long TotalPenaltyAmount { get; set; }

    [JsonPropertyName("totalRefundAmount")]
    public long TotalRefundAmount { get; set; }

    [JsonPropertyName("tickets")]
    public List<SnappTripPenaltyTicket> Tickets { get; set; } = new();
}

internal sealed class SnappTripPenaltyTicket
{
    [JsonPropertyName("ticketSerial")]
    public string TicketSerial { get; set; } = string.Empty;

    [JsonPropertyName("passengerName")]
    public string? PassengerName { get; set; }

    [JsonPropertyName("passengerType")]
    public string? PassengerType { get; set; }

    [JsonPropertyName("penaltyPercent")]
    public int? PenaltyPercent { get; set; }

    [JsonPropertyName("penaltyAmount")]
    public long PenaltyAmount { get; set; }

    [JsonPropertyName("refundAmount")]
    public long RefundAmount { get; set; }

    [JsonPropertyName("ticketPrice")]
    public long TicketPrice { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("noShow")]
    public bool NoShow { get; set; }

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
}

internal sealed class SnappTripCancelRequest
{
    [JsonPropertyName("trackingCode")]
    public string TrackingCode { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = string.Empty;

    [JsonPropertyName("ticketSerials")]
    public List<string> TicketSerials { get; set; } = new();

    [JsonPropertyName("reasonId")]
    public int ReasonId { get; set; }
}

internal sealed class SnappTripCancelResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("id")]
    public long? Id { get; set; }
}
