using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class ShopProduct
{
    private readonly List<ShopProductVariant> _variantOfferings = [];

    private ShopProduct() { }

    public Guid Id { get; private set; }
    public Guid ShopId { get; private set; }
    public Guid ProductId { get; private set; }
    public long Price { get; private set; }
    public long DiscountedPrice { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<ShopProductVariant> VariantOfferings => _variantOfferings.AsReadOnly();

    public static ShopProduct Create(Guid shopId, Guid productId, long price, long discountedPrice, string? description = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            ProductId = productId,
            Price = price,
            DiscountedPrice = discountedPrice,
            Description = description,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public void UpdateDetails(long price, long discountedPrice, string? description)
    {
        Price = price;
        DiscountedPrice = discountedPrice;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Enable()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
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

    public ShopProductVariant AddVariantOffering(
        Guid productVariantId,
        long priceMinor,
        long? discountedPriceMinor,
        bool isActive)
    {
        EnsureNotDeleted();

        if (_variantOfferings.Any(v => v.ProductVariantId == productVariantId && !v.IsDeleted))
            throw new StoreDomainException("این تنوع محصول قبلاً برای این محصول فروشگاه ثبت شده است", "SHOP_PRODUCT_VARIANT_EXISTS");

        var variantOffering = ShopProductVariant.Create(Id, productVariantId, priceMinor, discountedPriceMinor, isActive);
        _variantOfferings.Add(variantOffering);
        UpdatedAt = DateTimeOffset.UtcNow;

        return variantOffering;
    }

    public ShopProductVariant UpdateVariantOffering(
        Guid productVariantId,
        long priceMinor,
        long? discountedPriceMinor,
        bool isActive)
    {
        EnsureNotDeleted();

        var variantOffering = GetActiveVariantOffering(productVariantId);
        variantOffering.UpdateDetails(priceMinor, discountedPriceMinor, isActive);
        UpdatedAt = DateTimeOffset.UtcNow;

        return variantOffering;
    }

    public void EnableVariantOffering(Guid productVariantId)
    {
        EnsureNotDeleted();

        var variantOffering = GetActiveVariantOffering(productVariantId);
        variantOffering.Enable();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DisableVariantOffering(Guid productVariantId)
    {
        EnsureNotDeleted();

        var variantOffering = GetActiveVariantOffering(productVariantId);
        variantOffering.Disable();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveVariantOffering(Guid productVariantId)
    {
        EnsureNotDeleted();

        var variantOffering = GetActiveVariantOffering(productVariantId);
        variantOffering.SoftDelete();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private ShopProductVariant GetActiveVariantOffering(Guid productVariantId)
        => _variantOfferings.FirstOrDefault(v => v.ProductVariantId == productVariantId && !v.IsDeleted)
           ?? throw new StoreDomainException("تنوع محصول فروشگاه یافت نشد", "SHOP_PRODUCT_VARIANT_NOT_FOUND");

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new StoreDomainException("محصول فروشگاه حذف شده است", "SHOP_PRODUCT_DELETED");
    }
}
