using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class Banner
{
    private Banner() { }

    public int Id { get; private set; }
    public int ModuleId { get; private set; }                           // FK → StoreModule
    public string Title { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;
    public string? LinkUrl { get; private set; }
    public BannerType BannerType { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? EndDate { get; private set; }

    public static Banner Create(
        int moduleId, string title, string imageUrl, BannerType type,
        string? linkUrl = null, int sortOrder = 0,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
        => new()
        {
            ModuleId = moduleId,
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
