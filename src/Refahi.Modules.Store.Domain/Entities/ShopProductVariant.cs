using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class ShopProductVariant
{
    private ShopProductVariant() { }

    public Guid Id { get; private set; }
    public Guid ShopProductId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public long PriceMinor { get; private set; }
    public long? DiscountedPriceMinor { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ShopProductVariant Create(
        Guid shopProductId,
        Guid productVariantId,
        long priceMinor,
        long? discountedPriceMinor,
        bool isActive)
    {
        ValidatePrice(priceMinor, discountedPriceMinor);

        return new ShopProductVariant
        {
            Id = Guid.NewGuid(),
            ShopProductId = shopProductId,
            ProductVariantId = productVariantId,
            PriceMinor = priceMinor,
            DiscountedPriceMinor = discountedPriceMinor,
            IsActive = isActive,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    internal void UpdateDetails(long priceMinor, long? discountedPriceMinor, bool isActive)
    {
        EnsureNotDeleted();
        ValidatePrice(priceMinor, discountedPriceMinor);

        PriceMinor = priceMinor;
        DiscountedPriceMinor = discountedPriceMinor;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void Enable()
    {
        EnsureNotDeleted();
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void Disable()
    {
        EnsureNotDeleted();
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void SoftDelete()
    {
        EnsureNotDeleted();
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidatePrice(long priceMinor, long? discountedPriceMinor)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت باید بیشتر از صفر باشد", "INVALID_PRICE");

        if (discountedPriceMinor is <= 0)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید بیشتر از صفر باشد", "INVALID_DISCOUNTED_PRICE");

        if (discountedPriceMinor >= priceMinor)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید کمتر از قیمت اصلی باشد", "INVALID_DISCOUNTED_PRICE");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new StoreDomainException("تنوع محصول فروشگاه حذف شده است", "SHOP_PRODUCT_VARIANT_DELETED");
    }
}
