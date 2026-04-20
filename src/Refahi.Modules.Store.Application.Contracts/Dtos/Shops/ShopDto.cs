namespace Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

public sealed record ShopDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? CoverImageUrl,
    string ShopType,
    string Status,
    Guid ProviderId,
    int? ProvinceId,
    int? CityId,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? ManagerName,
    string? ManagerPhone,
    string? RepresentativeName,
    string? RepresentativePhone,
    string? ContactPhone,
    string? Description,
    bool IsPopular,
    DateTimeOffset CreatedAt);

public sealed record ShopSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string ShopType,
    string Status,
    int? ProvinceId,
    int? CityId,
    bool IsPopular);
