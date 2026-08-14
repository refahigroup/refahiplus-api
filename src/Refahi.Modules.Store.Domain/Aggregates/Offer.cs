using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Offer
{
    private Offer() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid ShopId { get; private set; }
    public Guid? ProductVariantId { get; private set; }
    public Guid? ProductSessionId { get; private set; }
    public long OriginalPriceMinor { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public long FinalPriceMinor { get; private set; }
    public DateTimeOffset StartDateUtc { get; private set; }
    public DateTimeOffset? EndDateUtc { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public static Offer Create(
        Guid supplierId,
        Guid productId,
        Guid shopId,
        Guid? productVariantId,
        Guid? productSessionId,
        long originalPriceMinor,
        decimal discountPercent,
        DateTimeOffset startDateUtc,
        DateTimeOffset? endDateUtc
    )
    {
        Validate(supplierId, productId, shopId, originalPriceMinor, discountPercent, startDateUtc, endDateUtc);
        var now = DateTimeOffset.UtcNow;
        return new Offer
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            ProductId = productId,
            ShopId = shopId,
            ProductVariantId = productVariantId,
            ProductSessionId = productSessionId,
            OriginalPriceMinor = originalPriceMinor,
            DiscountPercent = discountPercent,
            FinalPriceMinor = CalculateFinalPrice(originalPriceMinor, discountPercent),
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            IsActive = false,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(
        long originalPriceMinor,
        decimal discountPercent,
        DateTimeOffset startDateUtc,
        DateTimeOffset? endDateUtc
    )
    {
        if (IsDeleted)
            throw new StoreDomainException("پیشنهاد حذف شده است", "OFFER_DELETED");
        Validate(SupplierId, ProductId, ShopId, originalPriceMinor, discountPercent, startDateUtc, endDateUtc);
        OriginalPriceMinor = originalPriceMinor;
        DiscountPercent = discountPercent;
        FinalPriceMinor = CalculateFinalPrice(originalPriceMinor, discountPercent);
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        if (IsDeleted)
            throw new StoreDomainException("پیشنهاد حذف شده قابل فعال‌سازی نیست", "OFFER_DELETED");
        if (EndDateUtc.HasValue && EndDateUtc.Value <= DateTimeOffset.UtcNow)
            throw new StoreDomainException(
                "بازه زمانی پیشنهاد منقضی شده است",
                "OFFER_WINDOW_EXPIRED"
            );
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsEffectiveAt(DateTimeOffset atUtc) =>
        IsActive
        && !IsDeleted
        && StartDateUtc <= atUtc
        && (!EndDateUtc.HasValue || atUtc < EndDateUtc.Value);

    public static long CalculateFinalPrice(long originalPriceMinor, decimal discountPercent)
    {
        if (originalPriceMinor <= 0)
            throw new StoreDomainException(
                "قیمت اصلی باید بیشتر از صفر باشد",
                "INVALID_OFFER_PRICE"
            );
        if (discountPercent is < 0 or > 100)
            throw new StoreDomainException(
                "درصد تخفیف باید بین صفر تا صد باشد",
                "INVALID_DISCOUNT_PERCENT"
            );
        if (GetDecimalScale(discountPercent) > 2)
            throw new StoreDomainException(
                "درصد تخفیف حداکثر می‌تواند دو رقم اعشار داشته باشد",
                "INVALID_DISCOUNT_SCALE"
            );
        try
        {
            var discount = checked(
                (long)
                    Math.Round(
                        checked(originalPriceMinor * discountPercent) / 100m,
                        0,
                        MidpointRounding.AwayFromZero
                    )
            );
            return checked(originalPriceMinor - discount);
        }
        catch (OverflowException)
        {
            throw new StoreDomainException(
                "محاسبه قیمت از محدوده مجاز خارج است",
                "OFFER_PRICE_OVERFLOW"
            );
        }
    }

    private static int GetDecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static void Validate(
        Guid supplierId,
        Guid productId,
        Guid shopId,
        long originalPriceMinor,
        decimal discountPercent,
        DateTimeOffset startDateUtc,
        DateTimeOffset? endDateUtc
    )
    {
        if (supplierId == Guid.Empty || productId == Guid.Empty || shopId == Guid.Empty)
            throw new StoreDomainException(
                "مختصات پیشنهاد نامعتبر است",
                "INVALID_OFFER_COORDINATE"
            );
        _ = CalculateFinalPrice(originalPriceMinor, discountPercent);
        if (endDateUtc.HasValue && startDateUtc >= endDateUtc.Value)
            throw new StoreDomainException(
                "زمان پایان باید بعد از زمان شروع باشد",
                "INVALID_OFFER_WINDOW"
            );
    }
}
