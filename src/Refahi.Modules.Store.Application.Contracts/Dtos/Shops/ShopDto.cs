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
    string? City,
    string? Address,
    string? Description,
    string? ContactPhone,
    bool IsPopular,
    DateTimeOffset CreatedAt);

public sealed record ShopSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string ShopType,
    string Status,
    string? City,
    bool IsPopular);
