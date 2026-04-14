namespace Refahi.Modules.Store.Application.Contracts.Dtos.Banners;

public sealed record BannerDto(
    int Id, string Title, string ImageUrl, string? LinkUrl,
    string BannerType, int SortOrder, bool IsActive,
    DateTimeOffset? StartDate, DateTimeOffset? EndDate);
