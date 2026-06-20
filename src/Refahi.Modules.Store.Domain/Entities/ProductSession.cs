using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

/// <summary>
/// ProductSession — سانس‌های محصول (v1.1)
/// برای محصولاتی که مدل فروش آن‌ها SessionBased است
/// </summary>
public sealed class ProductSession
{
    private ProductSession() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string? Title { get; private set; }
    public int Capacity { get; private set; }
    public int SoldCount { get; private set; }
    public long PriceAdjustment { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsCancelled { get; private set; }

    public int RemainingCapacity => Capacity - SoldCount;
    public bool IsAvailable => IsActive && !IsCancelled && SoldCount < Capacity;

    internal static ProductSession Create(
        Guid productId, DateOnly date, TimeOnly startTime, TimeOnly endTime,
        int capacity, string? title, long priceAdjustment)
    {
        if (endTime <= startTime)
            throw new StoreDomainException("زمان پایان باید بعد از زمان شروع باشد", "INVALID_SESSION_TIME_RANGE");

        if (capacity <= 0)
            throw new StoreDomainException("ظرفیت سانس باید بیشتر از صفر باشد", "INVALID_SESSION_CAPACITY");

        if (priceAdjustment < 0)
            throw new StoreDomainException("تفاوت قیمت نمی‌تواند منفی باشد", "INVALID_SESSION_PRICE_ADJUSTMENT");

        return new ProductSession
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Title = title,
            Capacity = capacity,
            SoldCount = 0,
            PriceAdjustment = priceAdjustment,
            IsActive = true,
            IsCancelled = false
        };
    }

    public void Sell(int quantity = 1)
    {
        if (!IsActive || IsCancelled)
            throw new StoreDomainException("سانس فعال نیست", "SESSION_NOT_ACTIVE");
        if (SoldCount + quantity > Capacity)
            throw new StoreDomainException("ظرفیت تکمیل", "SESSION_FULL");
        SoldCount += quantity;
    }

    public void UndoSell(int quantity = 1)
    {
        if (quantity > SoldCount)
            throw new StoreDomainException("تعداد نامعتبر", "INVALID_UNDO");
        SoldCount -= quantity;
    }

    public void Cancel() { IsCancelled = true; }
    public void Deactivate() { IsActive = false; }
    public void Activate() { IsActive = true; }

    public void UpdateInfo(int capacity, string? title, long priceAdjustment)
    {
        if (capacity <= 0)
            throw new StoreDomainException("ظرفیت سانس باید بیشتر از صفر باشد", "INVALID_SESSION_CAPACITY");

        if (capacity < SoldCount)
            throw new StoreDomainException("ظرفیت سانس نمی‌تواند کمتر از تعداد فروخته‌شده باشد", "SESSION_CAPACITY_BELOW_SOLD_COUNT");

        if (priceAdjustment < 0)
            throw new StoreDomainException("تفاوت قیمت نمی‌تواند منفی باشد", "INVALID_SESSION_PRICE_ADJUSTMENT");

        Capacity = capacity;
        Title = title;
        PriceAdjustment = priceAdjustment;
    }
}
