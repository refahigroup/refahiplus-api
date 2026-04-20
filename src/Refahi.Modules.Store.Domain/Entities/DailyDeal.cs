using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class DailyDeal
{
    private DailyDeal() { }

    public int Id { get; private set; }
    public int ModuleId { get; private set; }                           // FK → StoreModule
    public Guid ProductId { get; private set; }
    public int DiscountPercent { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public bool IsActive { get; private set; }

    public static DailyDeal Create(int moduleId, Guid productId, int discountPercent,
        DateTimeOffset startTime, DateTimeOffset endTime)
        => new()
        {
            ModuleId = moduleId,
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
