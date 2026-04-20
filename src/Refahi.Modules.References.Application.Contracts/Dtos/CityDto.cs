namespace Refahi.Modules.References.Application.Contracts.Dtos;

public sealed record CityDto(
    int Id,
    string Name,
    string Slug,
    int ProvinceId,
    string ProvinceName,
    int SortOrder,
    bool IsActive);
