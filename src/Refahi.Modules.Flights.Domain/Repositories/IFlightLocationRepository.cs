namespace Refahi.Modules.Flights.Domain.Repositories;

public sealed record FlightLocation(
    string Code, string Type, string CityCode, string CityNameFa, string CityNameEn,
    string CountryCode, string CountryNameFa, string CountryNameEn, int AirportCount,
    string? AirportNameFa, string? AirportNameEn);

public sealed record FlightAirportPresentation(
    string AirportCode,
    string CityNameFa,
    string AirportNameFa
);

public interface IFlightLocationRepository
{
    Task<IReadOnlyList<FlightLocation>> SearchAsync(bool isDomestic, string? query, int limit, CancellationToken ct);
    Task<FlightLocation?> ResolveAsync(bool isDomestic, string code, string type, CancellationToken ct);
    Task<IReadOnlyDictionary<string, FlightAirportPresentation>> GetAirportPresentationsAsync(
        IEnumerable<string?> airportCodes,
        CancellationToken ct
    );
}
