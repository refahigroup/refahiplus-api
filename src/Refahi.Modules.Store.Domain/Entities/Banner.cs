using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class Banner
{
    private Banner() { }

    public int Id { get; private set; }
    public int? ModuleId { get; private set; }
    public Guid? ShopId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;
    public string? LinkUrl { get; private set; }
    public BannerType BannerType { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? EndDate { get; private set; }

    public BannerOwnerType OwnerType =>
        ModuleId.HasValue ? BannerOwnerType.Module : BannerOwnerType.Shop;

    public string OwnerId =>
        ModuleId?.ToString()
        ?? ShopId?.ToString()
        ?? throw new StoreDomainException("بنر فاقد مالک معتبر است", "BANNER_OWNER_MISSING");

    public static Banner CreateForModule(
        int moduleId, string title, string imageUrl, BannerType type,
        string? linkUrl = null, int sortOrder = 0,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
        => new()
        {
            ModuleId = moduleId,
            ShopId = null,
            Title = title,
            ImageUrl = imageUrl,
            BannerType = type,
            LinkUrl = linkUrl,
            SortOrder = sortOrder,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate
        };

    public static Banner CreateForShop(
        Guid shopId, string title, string imageUrl, BannerType type,
        string? linkUrl = null, int sortOrder = 0,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
        => new()
        {
            ModuleId = null,
            ShopId = shopId,
            Title = title,
            ImageUrl = imageUrl,
            BannerType = type,
            LinkUrl = linkUrl,
            SortOrder = sortOrder,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate
        };

    public void Update(string title, string imageUrl, string? linkUrl,
        int sortOrder, bool isActive,
        DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        Title = title;
        ImageUrl = imageUrl;
        LinkUrl = linkUrl;
        SortOrder = sortOrder;
        IsActive = isActive;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void Delete() => IsDeleted = true;
}
