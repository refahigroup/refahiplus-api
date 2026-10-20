using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Queries;

public sealed record CityGeography(int CityId, string CityName, string CityNameEn,
    int ProvinceId, string ProvinceName, string CountryCode, string CountryName,
    string CountryNameEn, bool IsActive);

public sealed record GetCityGeographiesQuery(IReadOnlyCollection<int> CityIds)
    : IRequest<IReadOnlyList<CityGeography>>;

public interface ICityGeographyReader
{
    Task<IReadOnlyList<CityGeography>> ReadAsync(IReadOnlyCollection<int> cityIds, CancellationToken ct);
}
