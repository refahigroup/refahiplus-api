namespace Refahi.Modules.Flights.Application.Contracts.Providers;

public sealed record FlightReferenceGeography(int CityId, string CityNameFa, string CityNameEn,
    int ProvinceId, string ProvinceName, string CountryCode, string CountryNameFa, string CountryNameEn);

public interface IFlightGeographyService
{
    Task<IReadOnlyDictionary<string, FlightReferenceGeography>> ReadAsync(
        bool commercial, IReadOnlyCollection<string>? codes, CancellationToken ct);
}
