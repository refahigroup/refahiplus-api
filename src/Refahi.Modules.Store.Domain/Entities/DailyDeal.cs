using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class DailyDeal
{
    private DailyDeal() { }

    public int Id { get; private set; }
    public int? ModuleId { get; private set; }
    public Guid? ShopId { get; private set; }
    public Guid ProductId { get; private set; }
    public int DiscountPercent { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public bool IsActive { get; private set; }

    public BannerOwnerType OwnerType =>
        ModuleId.HasValue ? BannerOwnerType.Module : BannerOwnerType.Shop;

    public string OwnerId =>
        ModuleId?.ToString()
        ?? ShopId?.ToString()
        ?? throw new StoreDomainException("تخفیف فاقد مالک معتبر است", "DAILY_DEAL_OWNER_MISSING");

    public static DailyDeal CreateForModule(int moduleId, Guid productId, int discountPercent,
        DateTimeOffset startTime, DateTimeOffset endTime)
        => new()
        {
            ModuleId = moduleId,
            ShopId = null,
            ProductId = productId,
            DiscountPercent = discountPercent,
            StartTime = startTime,
            EndTime = endTime,
            IsActive = true
        };

    public static DailyDeal CreateForShop(Guid shopId, Guid productId, int discountPercent,
        DateTimeOffset startTime, DateTimeOffset endTime)
        => new()
        {
            ModuleId = null,
            ShopId = shopId,
            ProductId = productId,
            DiscountPercent = discountPercent,
            StartTime = startTime,
            EndTime = endTime,
            IsActive = true
        };

    public bool IsCurrentlyActive() => IsActive
        && DateTimeOffset.UtcNow >= StartTime
        && DateTimeOffset.UtcNow <= EndTime;

    public void UpdateInfo(int discountPercent, DateTimeOffset startTime, DateTimeOffset endTime, bool isActive)
    {
        if (discountPercent <= 0 || discountPercent > 100)
            throw new StoreDomainException("درصد تخفیف باید بین ۱ تا ۱۰۰ باشد", "INVALID_DISCOUNT");
        if (endTime <= startTime)
            throw new StoreDomainException("زمان پایان باید بعد از زمان شروع باشد", "INVALID_TIME_RANGE");
        DiscountPercent = discountPercent;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = isActive;
    }

    public void Activate() { IsActive = true; }
    public void Deactivate() { IsActive = false; }
}
