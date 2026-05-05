namespace Refahi.Modules.Store.Application.Contracts.Dtos.Banners;

public sealed record AdminBannerDto(
    int Id, int? ModuleId, Guid? ShopId, string Title, string ImageUrl, string? LinkUrl,
    string BannerType, int SortOrder, bool IsActive,
    DateTimeOffset? StartDate, DateTimeOffset? EndDate);
