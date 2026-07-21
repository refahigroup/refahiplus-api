using MediatR;

namespace Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;

public sealed record GetFlightAirportsQuery(string? Query, int Limit = 20) : IRequest<GetFlightAirportsResponse>;

public sealed record GetFlightAirportsResponse(IReadOnlyCollection<FlightAirportDto> Airports);

public sealed record FlightAirportDto(
    string Code,
    string CityCode,
    string CityNameFa,
    string CityNameEn,
    string AirportNameFa,
    string AirportNameEn,
    string CountryCode,
    bool IsPopular,
    string CountryNameFa,
    string CountryNameEn,
    string? IcaoCode);
