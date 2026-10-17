namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolSummary(
    string ProviderKey,
    string Id,
    string Name,
    string? ImageUrl,
    string State,
    string City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude,
    bool IsSelling,
    int Gender,
    decimal? Rating,
    decimal? MaxDiscount,
    decimal? MinimumSalePrice,
    decimal? MinimumMarketPrice,
    string? PriceUnit
);
