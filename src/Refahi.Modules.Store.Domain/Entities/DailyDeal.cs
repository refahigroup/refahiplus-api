namespace Refahi.Modules.Store.Domain.Entities;

public sealed class DailyDeal
{
    private DailyDeal() { }

    public int Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int DiscountPercent { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public bool IsActive { get; private set; }

    public static DailyDeal Create(Guid productId, int discountPercent,
        DateTimeOffset startTime, DateTimeOffset endTime)
        => new()
        {
            ProductId = productId,
            DiscountPercent = discountPercent,
            StartTime = startTime,
            EndTime = endTime,
            IsActive = true
        };

    public bool IsCurrentlyActive() => IsActive
        && DateTimeOffset.UtcNow >= StartTime
        && DateTimeOffset.UtcNow <= EndTime;

    public void Deactivate() { IsActive = false; }
}
