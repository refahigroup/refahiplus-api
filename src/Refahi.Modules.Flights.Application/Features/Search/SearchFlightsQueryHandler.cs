using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Application.Features.Offers;
using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Flights.Application.Services.Airlines;

namespace Refahi.Modules.Flights.Application.Features.Search;

public sealed class SearchFlightsQueryHandler
    : IRequestHandler<SearchFlightsQuery, SearchFlightsResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan OfferTimeToLive = TimeSpan.FromMinutes(20);

    private readonly IFlightProviderFactory _providerFactory;
    private readonly IFlightOfferSnapshotRepository _offerSnapshotRepository;
    private readonly IFlightLocationRepository _locationRepository;
    private readonly IAirlineLogoResolver _airlineLogoResolver;

    public SearchFlightsQueryHandler(
        IFlightProviderFactory providerFactory,
        IFlightOfferSnapshotRepository offerSnapshotRepository,
        IFlightLocationRepository locationRepository,
        IAirlineLogoResolver airlineLogoResolver
    )
    {
        _providerFactory = providerFactory;
        _offerSnapshotRepository = offerSnapshotRepository;
        _locationRepository = locationRepository;
        _airlineLogoResolver = airlineLogoResolver;
    }

    public async Task<SearchFlightsResponse> Handle(
        SearchFlightsQuery request,
        CancellationToken cancellationToken
    )
    {
        var mode = request.IsDomestic ?? true;
        var origin = await _locationRepository.ResolveAsync(mode, request.Origin!, request.OriginType, cancellationToken);
        var destination = await _locationRepository.ResolveAsync(mode, request.Destination!, request.DestinationType, cancellationToken);
        if (!request.IsDomestic.HasValue && (origin is null || destination is null))
        {
            mode = false;
            origin = await _locationRepository.ResolveAsync(false, request.Origin!, request.OriginType, cancellationToken);
            destination = await _locationRepository.ResolveAsync(false, request.Destination!, request.DestinationType, cancellationToken);
        }
        if (origin is null || destination is null)
            throw InvalidRoute("مبدأ یا مقصد در فهرست مجاز نیست؛ لطفاً دوباره انتخاب کنید.");
        if (origin.CityCode == destination.CityCode)
            throw InvalidRoute("مبدأ و مقصد نمی‌توانند یک شهر باشند.");
        if (!mode && origin.CountryCode == "IR" && destination.CountryCode == "IR")
            throw InvalidRoute("برای مسیر بین دو شهر ایران، پرواز داخلی را انتخاب کنید.");
        var providerRequest = BuildProviderRequest(request, mode, origin, destination);
        var provider = _providerFactory.GetDefaultProvider();
        var providerResponse = await provider.SearchAsync(providerRequest, cancellationToken);

        if (!providerResponse.Success)
            throw new InvalidOperationException(BuildSafeProviderError(providerResponse.Error));

        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.Add(OfferTimeToLive);
        var publicOffers = new List<FlightOfferDto>();
        var airlinePresentations = await _airlineLogoResolver.ResolvePresentationsAsync(
            providerResponse.Offers.SelectMany(offer =>
                new[] { offer.ValidatingAirlineCode }
                    .Concat(offer.OriginDestinationOptions
                        .SelectMany(option => option.FlightSegments)
                        .SelectMany(segment => new[]
                        {
                            segment.MarketingAirlineCode,
                            segment.OperatingAirlineCode,
                        }))
            ),
            cancellationToken
        );
        var airportPresentations = await _locationRepository.GetAirportPresentationsAsync(
            providerResponse.Offers.SelectMany(offer =>
                offer.OriginDestinationOptions
                    .SelectMany(option => option.FlightSegments)
                    .SelectMany(segment => new[]
                    {
                        segment.DepartureAirportLocationCode,
                        segment.ArrivalAirportLocationCode,
                    })
            ),
            cancellationToken
        );

        foreach (var providerOffer in providerResponse.Offers)
        {
            ValidateProviderOffer(providerOffer);

            var offerToken = CreateOfferToken();
            var publicOffer = MapToPublicOffer(
                providerOffer,
                offerToken,
                expiresAtUtc,
                airlinePresentations,
                airportPresentations
            );
            var publicSnapshotJson = JsonSerializer.Serialize(publicOffer, JsonOptions);

            var snapshot = FlightOfferSnapshot.Create(
                offerToken,
                providerOffer.ProviderName,
                providerOffer.ProviderFareSourceCode,
                providerOffer.SearchId,
                providerOffer.ProviderTraceId ?? providerResponse.ProviderTraceId,
                providerOffer.TotalFare.TotalFare,
                providerOffer.TotalFare.TotalCommission,
                providerOffer.TotalFare.CustomerPayableAmountMinor,
                providerOffer.TotalFare.Currency,
                publicSnapshotJson,
                providerOffer.RawPayloadSnapshot ?? providerResponse.RawPayloadSnapshot,
                nowUtc,
                expiresAtUtc
            );

            await _offerSnapshotRepository.AddAsync(snapshot, cancellationToken);
            publicOffers.Add(publicOffer);
        }

        await _offerSnapshotRepository.SaveChangesAsync(cancellationToken);

        return new SearchFlightsResponse(
            expiresAtUtc,
            publicOffers,
            origin.CityNameFa,
            destination.CityNameFa
        );
    }

    private static FluentValidation.ValidationException InvalidRoute(string message) =>
        new([new FluentValidation.Results.ValidationFailure("Route", message)]);

    private static FlightSearchRequest BuildProviderRequest(
        SearchFlightsQuery request,
        bool isDomestic,
        FlightLocation originLocation,
        FlightLocation destinationLocation
    )
    {
        var origin = originLocation.Code;
        var destination = destinationLocation.Code;
        var airTripType = string.IsNullOrWhiteSpace(request.AirTripType)
            ? request.ReturnDate.HasValue
                ? "RoundTrip"
                : "OneWay"
            : request.AirTripType.Trim();

        var legs = new List<FlightSearchLeg>
        {
            new(request.DepartureDate!.Value, origin, destination, originLocation.Type, destinationLocation.Type),
        };

        if (request.ReturnDate.HasValue)
        {
            legs.Add(
                new FlightSearchLeg(
                    request.ReturnDate.Value,
                    destination,
                    origin,
                    destinationLocation.Type,
                    originLocation.Type
                )
            );
        }

        return new FlightSearchRequest(
            request.Adult,
            request.Child,
            request.Infant,
            isDomestic,
            legs,
            new FlightTravelPreference(
                request.CabinType.Trim(),
                airTripType,
                request.MaxStopsQuantity,
                NormalizeCodes(request.VendorExcludeCodes),
                NormalizeCodes(request.VendorPreferenceCodes)
            )
        );
    }

    private static IReadOnlyCollection<string>? NormalizeCodes(IReadOnlyCollection<string>? codes)
    {
        var normalized = codes
            ?.Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized is { Count: > 0 } ? normalized : null;
    }

    private static void ValidateProviderOffer(FlightFareOffer offer)
    {
        if (
            string.IsNullOrWhiteSpace(offer.ProviderFareSourceCode)
            || offer.TotalFare.TotalFare <= 0
            || offer.TotalFare.TotalCommission < 0
            || offer.TotalFare.CustomerPayableAmountMinor <= 0
            || offer.TotalFare.CustomerPayableAmountMinor
                != checked(offer.TotalFare.TotalFare + offer.TotalFare.TotalCommission)
            || !string.Equals(offer.TotalFare.Currency, "IRR", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException("اطلاعات قیمت پرواز از تامین‌کننده معتبر نیست.");
        }
    }

    private static FlightOfferDto MapToPublicOffer(
        FlightFareOffer offer,
        string offerToken,
        DateTime expiresAtUtc,
        IReadOnlyDictionary<string, AirlinePresentation> airlinePresentations,
        IReadOnlyDictionary<string, FlightAirportPresentation> airportPresentations
    )
    {
        var segments = offer
            .OriginDestinationOptions.SelectMany(option => option.FlightSegments)
            .Select(segment => MapSegment(segment, airlinePresentations, airportPresentations))
            .ToList();

        var firstSegment = segments.FirstOrDefault();
        var lastSegment = segments.LastOrDefault();
        var firstProviderSegment = offer
            .OriginDestinationOptions.SelectMany(option => option.FlightSegments)
            .FirstOrDefault();
        var totalDuration = offer
            .OriginDestinationOptions.Where(option => option.JourneyDurationPerMinute.HasValue)
            .Sum(option => option.JourneyDurationPerMinute!.Value);

        return new FlightOfferDto(
            offerToken,
            expiresAtUtc,
            offer.Direction,
            firstSegment?.DepartureAirportCode ?? string.Empty,
            firstSegment?.DepartureAirportCaption,
            lastSegment?.ArrivalAirportCode ?? string.Empty,
            lastSegment?.ArrivalAirportCaption,
            firstSegment?.DepartureDateTime,
            lastSegment?.ArrivalDateTime,
            offer.ValidatingAirlineCode ?? firstSegment?.MarketingAirlineCode,
            AirlineName(
                airlinePresentations,
                offer.ValidatingAirlineCode ?? firstSegment?.MarketingAirlineCode,
                offer.ValidatingAirlineCaption ?? firstSegment?.MarketingAirlineCaption
            ),
            firstSegment?.FlightNumber,
            firstSegment?.CabinClassCode,
            firstSegment?.CabinClassCaption,
            offer.FareType,
            totalDuration > 0 ? totalDuration : firstSegment?.DurationMinutes,
            Math.Max(0, segments.Count - 1),
            firstProviderSegment?.SeatsRemaining,
            firstProviderSegment?.Baggage,
            MapMoney(offer.TotalFare),
            segments,
            offer.PassengerFareBreakdowns.Select(MapPassengerFare).ToList(),
            AirlineLogo(
                airlinePresentations,
                offer.ValidatingAirlineCode ?? firstSegment?.MarketingAirlineCode
            )
        );
    }

    private static FlightSegmentDto MapSegment(
        FlightSegmentOffer segment,
        IReadOnlyDictionary<string, AirlinePresentation> airlinePresentations,
        IReadOnlyDictionary<string, FlightAirportPresentation> airportPresentations
    )
    {
        return new FlightSegmentDto(
            segment.DepartureAirportLocationCode,
            AirportCaption(
                airportPresentations,
                segment.DepartureAirportLocationCode,
                segment.DepartureAirportCaption
            ),
            segment.ArrivalAirportLocationCode,
            AirportCaption(
                airportPresentations,
                segment.ArrivalAirportLocationCode,
                segment.ArrivalAirportCaption
            ),
            segment.DepartureDateTime,
            segment.ArrivalDateTime,
            segment.FlightNumber,
            segment.MarketingAirlineCode,
            AirlineName(
                airlinePresentations,
                segment.MarketingAirlineCode,
                segment.MarketingAirlineCaption
            ),
            segment.OperatingAirlineCode,
            AirlineName(
                airlinePresentations,
                segment.OperatingAirlineCode,
                segment.OperatingAirlineCaption
            ),
            segment.CabinClassCode,
            segment.CabinClassCaption,
            segment.ResBookDesigCode,
            segment.JourneyDurationPerMinute,
            segment.SeatsRemaining,
            segment.StopQuantity,
            segment.Baggage,
            segment.IsCharter,
            segment.IsReturn,
            AirlineLogo(airlinePresentations, segment.MarketingAirlineCode),
            AirlineLogo(airlinePresentations, segment.OperatingAirlineCode)
        );
    }

    private static string? AirlineLogo(
        IReadOnlyDictionary<string, AirlinePresentation> presentations,
        string? airlineCode
    )
    {
        if (string.IsNullOrWhiteSpace(airlineCode))
            return null;

        return presentations.GetValueOrDefault(airlineCode.Trim().ToUpperInvariant())?.LogoUrl;
    }

    private static string? AirlineName(
        IReadOnlyDictionary<string, AirlinePresentation> presentations,
        string? airlineCode,
        string? providerCaption
    )
    {
        if (
            !string.IsNullOrWhiteSpace(providerCaption)
            && !string.Equals(providerCaption.Trim(), airlineCode?.Trim(), StringComparison.OrdinalIgnoreCase)
        )
            return providerCaption.Trim();

        if (string.IsNullOrWhiteSpace(airlineCode))
            return null;

        return presentations
            .GetValueOrDefault(airlineCode.Trim().ToUpperInvariant())
            ?.Name;
    }

    private static string? AirportCaption(
        IReadOnlyDictionary<string, FlightAirportPresentation> presentations,
        string airportCode,
        string? providerCaption
    )
    {
        if (presentations.TryGetValue(airportCode.Trim(), out var presentation))
            return string.Equals(
                presentation.CityNameFa,
                presentation.AirportNameFa,
                StringComparison.OrdinalIgnoreCase
            )
                ? presentation.CityNameFa
                : $"{presentation.CityNameFa}، {presentation.AirportNameFa}";

        return !string.IsNullOrWhiteSpace(providerCaption)
               && !string.Equals(providerCaption.Trim(), airportCode.Trim(), StringComparison.OrdinalIgnoreCase)
            ? providerCaption.Trim()
            : null;
    }

    private static FlightPassengerFareDto MapPassengerFare(FlightPassengerFareBreakdown fare)
    {
        return new FlightPassengerFareDto(fare.PassengerType, fare.Quantity, MapMoney(fare.Fare));
    }

    private static FlightMoneyDto MapMoney(FlightMoney money)
    {
        return new FlightMoneyDto(
            money.BaseFare,
            money.TotalFare,
            money.TotalTax,
            money.TotalCommission,
            money.ServiceTax,
            money.Currency,
            money.CustomerPayableAmountMinor
        );
    }

    private static string CreateOfferToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string BuildSafeProviderError(FlightProviderError? error)
    {
        if (
            error?.Code is not null
            && error.Code.Contains("PASSENGER", StringComparison.OrdinalIgnoreCase)
        )
            return "ترکیب یا تعداد مسافران توسط تأمین‌کننده پذیرفته نشد.";

        return "جست‌وجوی پرواز در تأمین‌کننده انجام نشد. لطفاً اطلاعات جست‌وجو را بررسی و دوباره تلاش کنید.";
    }
}
