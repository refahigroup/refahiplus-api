namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolSearchQuery(
    string ProviderKey,
    string? StateId = null,
    string? CityId = null,
    string? AreaId = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? Search = null,
    int? Gender = null,
    string? Sort = null,
    int Page = 1,
    int Limit = 10,
    bool? ActiveOnly = null
);
