using System.Globalization;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;

internal static class SnappTripFlightMapper
{
    public const string ProviderName = "SnappTrip";

    public static SnappTripSearchRequest ToSnappTripRequest(FlightSearchRequest request)
    {
        return new SnappTripSearchRequest
        {
            Adult = request.Adult,
            Child = request.Child,
            Infant = request.Infant,
            IsDomestic = request.IsDomestic,
            OriginDestinationInformations = request
                .OriginDestinationInformations.Select(
                    leg => new SnappTripOriginDestinationInformation
                    {
                        DepartureDate = leg.DepartureDate.ToString("yyyy-MM-dd"),
                        OriginLocationCode = leg.OriginLocationCode,
                        DestinationLocationCode = leg.DestinationLocationCode,
                        OriginType = NormalizeLocationType(leg.OriginType),
                        DestinationType = NormalizeLocationType(leg.DestinationType),
                    }
                )
                .ToList(),
            TravelPreference = request.TravelPreference is null
                ? null
                : new SnappTripTravelPreference
                {
                    CabinType = NormalizeCabinType(request.TravelPreference.CabinType),
                    AirTripType = NormalizeAirTripType(request.TravelPreference.AirTripType),
                    MaxStopsQuantity =
                        request.TravelPreference.MaxStopsQuantity?.ToString(
                            CultureInfo.InvariantCulture
                        ) ?? "ALL",
                    VendorExcludeCodes = request.TravelPreference.VendorExcludeCodes?.ToList(),
                    VendorPreferenceCodes =
                        request.TravelPreference.VendorPreferenceCodes?.ToList(),
                },
        };
    }

    private static string NormalizeLocationType(string value)
    {
        return value.Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "AIRPORT" => "AIRPORT",
            "CITY" => "CITY",
            var normalized => normalized,
        };
    }

    private static string NormalizeCabinType(string value)
    {
        return value.Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "ECONOMY" => "ECONOMY",
            "BUSINESS" => "BUSINESS",
            "FIRST" => "FIRST",
            "FIRSTCLASS" => "FIRST",
            "FIRST_CLASS" => "FIRST",
            var normalized => normalized,
        };
    }

    private static string NormalizeAirTripType(string value)
    {
        return value
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant() switch
        {
            "RETURN" => "RETURN",
            "ROUNDTRIP" => "RETURN",
            "ONEWAY" => "ONEWAY",
            "ONEWAYTRIP" => "ONEWAY",
            var normalized => normalized,
        };
    }

    public static FlightSearchResponse ToFlightResponse(
        SnappTripSearchResponse response,
        string? maskedRawPayload,
        decimal charterCommissionPercent
    )
    {
        var searchId = response.SearchId?.ToString();
        var offers = response
            .PricedItineraries.Where(item => !string.IsNullOrWhiteSpace(item.FareSourceCode))
            .Select(item =>
                ToFlightFareOffer(
                    item,
                    searchId,
                    maskedRawPayload,
                    charterCommissionPercent
                )
            )
            .ToList();

        return new FlightSearchResponse(
            response.Success,
            ProviderName,
            searchId,
            offers,
            response.Error is null
                ? null
                : new FlightProviderError(response.Error.Code, response.Error.Message),
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    public static SnappTripBookRequest ToSnappTripRequest(FlightBookRequest request)
    {
        return new SnappTripBookRequest
        {
            FareSourceCode = request.ProviderFareSourceCode,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Passengers = request
                .Passengers.Select(passenger => new SnappTripBookPassenger
                {
                    NationalityCode = passenger.NationalityCode.Trim().ToUpperInvariant(),
                    NationalId = NormalizeOptional(passenger.NationalId),
                    FirstName = passenger.FirstName.Trim(),
                    LastName = passenger.LastName.Trim(),
                    Gender = passenger.Gender.Trim().ToUpperInvariant(),
                    Birthday = passenger.Birthday.ToString("yyyy-MM-dd"),
                    PassengerType = passenger.PassengerType.Trim().ToUpperInvariant(),
                    PassportInfo = passenger.PassportInfo is null
                        ? null
                        : new SnappTripPassportInfo
                        {
                            CountryCode = NormalizeOptional(passenger.PassportInfo.CountryCode)
                                ?.ToUpperInvariant(),
                            IssueDate = passenger.PassportInfo.IssueDate?.ToString("yyyy-MM-dd"),
                            ExpireDate = passenger.PassportInfo.ExpireDate?.ToString("yyyy-MM-dd"),
                            Number = NormalizeOptional(passenger.PassportInfo.Number),
                        },
                })
                .ToList(),
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static FlightBookResponse ToFlightResponse(
        SnappTripBookResponse response,
        string? maskedRawPayload
    )
    {
        return new FlightBookResponse(
            response.TrackingCode,
            response.CheckoutUrl,
            response.BookId,
            response.PaymentCurrency,
            response.PaymentAmount,
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    public static SnappTripIssueRequest ToSnappTripRequest(FlightIssueRequest request)
    {
        return new SnappTripIssueRequest { BookId = request.BookId };
    }

    public static FlightIssueResponse ToFlightResponse(
        SnappTripIssueResponse response,
        string? maskedRawPayload
    )
    {
        return new FlightIssueResponse(response.Status, RawPayloadSnapshot: maskedRawPayload);
    }

    public static FlightInquiryResponse ToFlightResponse(
        SnappTripInquiryResponse response,
        string? maskedRawPayload
    )
    {
        return new FlightInquiryResponse(
            response.Status,
            response.TrackingCode,
            response.Pnr,
            response.Buyer is null
                ? null
                : new FlightInquiryBuyer(
                    response.Buyer.FirstName,
                    response.Buyer.LastName,
                    response.Buyer.PhoneNumber,
                    response.Buyer.Email,
                    response.Buyer.ReceiverPhoneNumber,
                    response.Buyer.ReceiverEmail
                ),
            response
                .Tickets.Select(ticket => new FlightInquiryTicket(
                    ticket.Serial,
                    ticket.Pnr,
                    ticket.PassengerName,
                    ticket.PassengerType,
                    ticket.Direction,
                    ticket.DocumentType,
                    ticket.DocumentId,
                    ticket
                        .CancellationRoutes.Where(route =>
                            !string.IsNullOrWhiteSpace(route.RouteId)
                        )
                        .Select(route => new FlightCancellationRoute(
                            route.RouteId,
                            route.Direction,
                            route.Origin,
                            null,
                            route.Destination,
                            null
                        ))
                        .ToList()
                ))
                .ToList(),
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    public static SnappTripPenaltyRequest ToSnappTripRequest(FlightCancellationQuoteRequest request)
    {
        return new SnappTripPenaltyRequest
        {
            TrackingCode = request.TrackingCode,
            RouteId = request.RouteId,
            TicketSerials = request.TicketSerials.ToList(),
            ReasonId = request.ReasonId,
        };
    }

    public static FlightCancellationQuoteResponse ToFlightResponse(
        SnappTripPenaltyResponse response,
        string? maskedRawPayload
    )
    {
        return new FlightCancellationQuoteResponse(
            response.PenaltyStatus,
            response.IsEligibleForCancellation,
            response.IsValid,
            response.RouteId,
            response.Currency,
            response.TotalAmount,
            response.TotalPenaltyAmount,
            response.TotalRefundAmount,
            response
                .Tickets.Select(ticket => new FlightCancellationTicketQuote(
                    ticket.TicketSerial,
                    ticket.PassengerName,
                    ticket.PassengerType,
                    ticket.PenaltyPercent,
                    ticket.PenaltyAmount,
                    ticket.RefundAmount,
                    ticket.TicketPrice,
                    ticket.Currency,
                    ticket.NoShow,
                    ticket.IsValid
                ))
                .ToList(),
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    public static SnappTripCancelRequest ToSnappTripRequest(FlightCancellationSubmitRequest request)
    {
        return new SnappTripCancelRequest
        {
            TrackingCode = request.TrackingCode,
            Phone = request.Phone,
            RouteId = request.RouteId,
            TicketSerials = request.TicketSerials.ToList(),
            ReasonId = request.ReasonId,
        };
    }

    public static FlightCancellationSubmitResponse ToFlightResponse(
        SnappTripCancelResponse response,
        string? maskedRawPayload
    )
    {
        return new FlightCancellationSubmitResponse(
            response.Message,
            response.Status,
            response.Id?.ToString(),
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    private static FlightFareOffer ToFlightFareOffer(
        SnappTripPricedItinerary itinerary,
        string? searchId,
        string? maskedRawPayload,
        decimal charterCommissionPercent
    )
    {
        var totalFare = itinerary.AirItineraryPricingInfo?.ItinTotalFare;
        var serviceAmount = totalFare?.TotalFare ?? 0;
        var isCharter = itinerary
            .OriginDestinationOptions.SelectMany(option => option.FlightSegments)
            .Any(segment => segment.IsCharter == true);
        var commissionAmount = isCharter
            ? CalculatePercentage(serviceAmount, charterCommissionPercent)
            : totalFare?.TotalCommission ?? 0;
        var customerPayableAmount = CheckedPayableAmount(serviceAmount, commissionAmount);
        var passengerFareBreakdowns = itinerary.AirItineraryPricingInfo?.PtcFareBreakdown ?? [];
        var mappedPassengerFares = isCharter
            ? ToCharterPassengerFareBreakdowns(passengerFareBreakdowns, commissionAmount)
            : passengerFareBreakdowns
                .Select(breakdown => ToPassengerFareBreakdown(breakdown))
                .ToList();

        return new FlightFareOffer(
            ProviderName,
            searchId,
            itinerary.FareSourceCode,
            itinerary.DirectionInd,
            itinerary.ValidatingAirlineCode,
            null,
            new FlightMoney(
                totalFare?.BaseFare ?? 0,
                totalFare?.TotalFare ?? 0,
                totalFare?.TotalTax ?? 0,
                commissionAmount,
                totalFare?.ServiceTax ?? 0,
                totalFare?.Currency ?? "IRR",
                customerPayableAmount
            ),
            itinerary.OriginDestinationOptions.Select(ToFlightOption).ToList(),
            mappedPassengerFares,
            itinerary.AirItineraryPricingInfo?.FareType,
            RawPayloadSnapshot: maskedRawPayload
        );
    }

    private static FlightOriginDestinationOption ToFlightOption(
        SnappTripOriginDestinationOption option
    )
    {
        return new FlightOriginDestinationOption(
            option.FlightSegments.Select(ToFlightSegment).ToList(),
            option.JourneyDurationPerMinute,
            option.ConnectionTimePerMinute
        );
    }

    private static FlightSegmentOffer ToFlightSegment(SnappTripFlightSegment segment)
    {
        return new FlightSegmentOffer(
            ProviderSegmentId: null,
            DepartureAirportLocationCode: segment.DepartureAirportLocationCode ?? string.Empty,
            DepartureAirportCaption: null,
            ArrivalAirportLocationCode: segment.ArrivalAirportLocationCode ?? string.Empty,
            ArrivalAirportCaption: null,
            DepartureDateTime: ParseDateTime(segment.DepartureDateTime),
            ArrivalDateTime: ParseDateTime(segment.ArrivalDateTime),
            FlightNumber: segment.FlightNumber,
            MarketingAirlineCode: segment.MarketingAirlineCode,
            MarketingAirlineCaption: null,
            OperatingAirlineCode: segment.OperatingAirline?.Code,
            OperatingAirlineCaption: null,
            OperatingAirlineFlightNumber: segment.OperatingAirline?.FlightNumber,
            Equipment: segment.OperatingAirline?.Equipment,
            CabinClassCode: segment.CabinClassCode,
            CabinClassCaption: null,
            ResBookDesigCode: segment.ResBookDesigCode,
            JourneyDurationPerMinute: segment.JourneyDurationPerMinute,
            ConnectionTimePerMinute: segment.ConnectionTimePerMinute,
            SeatsRemaining: segment.SeatsRemaining,
            StopQuantity: segment.StopQuantity,
            Baggage: segment.Baggage,
            IsCharter: segment.IsCharter,
            IsReturn: segment.IsReturn
        );
    }

    private static FlightPassengerFareBreakdown ToPassengerFareBreakdown(
        SnappTripPtcFareBreakdown breakdown,
        long? commissionOverride = null
    )
    {
        var fare = breakdown.PassengerFare;
        var serviceAmount = fare?.TotalFare ?? 0;
        var commissionAmount = commissionOverride ?? fare?.Commission ?? 0;

        return new FlightPassengerFareBreakdown(
            breakdown.PassengerTypeQuantity?.PassengerType,
            breakdown.PassengerTypeQuantity?.Quantity ?? 0,
            new FlightMoney(
                fare?.BaseFare ?? 0,
                fare?.TotalFare ?? 0,
                0,
                commissionAmount,
                fare?.ServiceTax ?? 0,
                fare?.Currency ?? "IRR",
                CheckedPayableAmount(serviceAmount, commissionAmount)
            )
        );
    }

    private static IReadOnlyCollection<FlightPassengerFareBreakdown> ToCharterPassengerFareBreakdowns(
        IReadOnlyList<SnappTripPtcFareBreakdown> breakdowns,
        long totalCommissionAmount
    )
    {
        if (breakdowns.Count == 0)
            return [];

        var weights = breakdowns
            .Select((breakdown, index) => new
            {
                Index = index,
                Amount = breakdown.PassengerFare?.TotalFare ?? 0,
            })
            .ToList();

        if (weights.Any(item => item.Amount < 0))
            throw InvalidProviderPricing();

        var totalWeight = weights.Sum(item => (decimal)item.Amount);
        if (totalWeight <= 0)
        {
            if (totalCommissionAmount > 0)
                throw InvalidProviderPricing();

            return breakdowns
                .Select(breakdown => ToPassengerFareBreakdown(breakdown, 0))
                .ToList();
        }

        var allocations = new long[breakdowns.Count];
        var fractions = new List<(int Index, decimal Fraction)>(breakdowns.Count);
        long allocatedAmount = 0;

        foreach (var weight in weights)
        {
            var exactShare = totalCommissionAmount * ((decimal)weight.Amount / totalWeight);
            var floorShare = checked((long)decimal.Floor(exactShare));
            allocations[weight.Index] = floorShare;
            allocatedAmount = checked(allocatedAmount + floorShare);
            fractions.Add((weight.Index, exactShare - floorShare));
        }

        var remainder = totalCommissionAmount - allocatedAmount;
        foreach (
            var allocation in fractions
                .OrderByDescending(item => item.Fraction)
                .ThenBy(item => item.Index)
                .Take(checked((int)remainder))
        )
        {
            allocations[allocation.Index] = checked(allocations[allocation.Index] + 1);
        }

        return breakdowns
            .Select((breakdown, index) =>
                ToPassengerFareBreakdown(breakdown, allocations[index])
            )
            .ToList();
    }

    private static long CalculatePercentage(long amount, decimal percent)
    {
        if (
            amount < 0
            || percent is < 0m or > 100m
            || decimal.Round(percent, 2) != percent
        )
            throw InvalidProviderPricing();

        try
        {
            return checked(
                (long)Math.Round(
                    amount * percent / 100m,
                    0,
                    MidpointRounding.AwayFromZero
                )
            );
        }
        catch (OverflowException ex)
        {
            throw InvalidProviderPricing(ex);
        }
    }

    private static long CheckedPayableAmount(long serviceAmount, long commissionAmount)
    {
        if (serviceAmount < 0 || commissionAmount < 0)
            throw InvalidProviderPricing();

        try
        {
            return checked(serviceAmount + commissionAmount);
        }
        catch (OverflowException ex)
        {
            throw InvalidProviderPricing(ex);
        }
    }

    private static InvalidOperationException InvalidProviderPricing(Exception? inner = null) =>
        new("اطلاعات قیمت پرواز از تامین‌کننده معتبر نیست.", inner);

    private static DateTime? ParseDateTime(string? value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
