using MediatR;
using Refahi.Modules.Flights.Application.Features.Offers;

namespace Refahi.Modules.Flights.Application.Features.Search;

public sealed record SearchFlightsQuery(
    string? Origin,
    string? Destination,
    DateOnly? DepartureDate,
    DateOnly? ReturnDate,
    int Adult,
    int Child,
    int Infant,
    string CabinType,
    string? AirTripType,
    bool? IsDomestic,
    int? MaxStopsQuantity,
    IReadOnlyCollection<string>? VendorExcludeCodes,
    IReadOnlyCollection<string>? VendorPreferenceCodes) : IRequest<SearchFlightsResponse>;

public sealed record SearchFlightsResponse(
    DateTime OffersExpireAtUtc,
    IReadOnlyCollection<FlightOfferDto> Offers);
