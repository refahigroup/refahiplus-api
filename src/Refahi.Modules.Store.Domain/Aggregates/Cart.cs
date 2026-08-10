using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Cart
{
    private Cart()
    {
        _items = new List<CartItem>();
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int ModuleId { get; private set; } // FK → StoreModule — per-module cart
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<CartItem> _items;
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public long TotalMinor => _items.Sum(i => i.UnitPriceMinor * i.Quantity);

    // --- Factory ---
    public static Cart Create(Guid userId, int moduleId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ModuleId = moduleId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // --- Behaviors ---
    public void AddItem(
        Guid shopId,
        Guid productId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long unitPriceMinor
    )
    {
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (unitPriceMinor <= 0)
            throw new StoreDomainException("قیمت واحد باید بیشتر از صفر باشد", "INVALID_PRICE");

        var existing = _items.FirstOrDefault(i =>
            i.ShopId == shopId
            && i.ProductId == productId
            && i.VariantId == variantId
            && i.SessionId == sessionId
            && i.UsageDate == usageDate
        );

        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
        }
        else
        {
            _items.Add(
                CartItem.Create(
                    Id,
                    shopId,
                    productId,
                    variantId,
                    sessionId,
                    usageDate,
                    quantity,
                    unitPriceMinor
                )
            );
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateItemQuantity(Guid itemId, int newQuantity)
    {
        var item =
            _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        item.UpdateQuantity(newQuantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RefreshOfferItem(
        Guid itemId,
        Guid offerId,
        Guid productId,
        Guid shopId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long originalUnitPriceMinor,
        long finalUnitPriceMinor
    )
    {
        var item =
            _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        if (_items.Any(x => x.Id != itemId && x.OfferId.HasValue && x.ShopId != shopId))
            throw new StoreDomainException(
                "تمامی محصولات باید از یک فروشگاه باشند",
                "MIXED_SHOP_ITEMS"
            );
        item.RefreshOffer(
            offerId,
            productId,
            shopId,
            variantId,
            sessionId,
            usageDate,
            quantity,
            originalUnitPriceMinor,
            finalUnitPriceMinor
        );
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddOfferItem(
        Guid shopId,
        Guid productId,
        Guid offerId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long originalUnitPriceMinor,
        long finalUnitPriceMinor
    )
    {
        if (
            offerId == Guid.Empty
            || quantity <= 0
            || originalUnitPriceMinor <= 0
            || finalUnitPriceMinor < 0
        )
            throw new StoreDomainException(
                "اطلاعات پیشنهاد سبد خرید معتبر نیست",
                "INVALID_CART_OFFER"
            );
        if (_items.Any(x => !x.OfferId.HasValue))
            throw new StoreDomainException(
                "آیتم قدیمی در عملیات سبد v3 پشتیبانی نمی‌شود",
                "LEGACY_CART_ITEM_NOT_SUPPORTED"
            );
        if (_items.Any(x => x.OfferId.HasValue && x.ShopId != shopId))
            throw new StoreDomainException(
                "تمامی محصولات باید از یک فروشگاه باشند",
                "MIXED_SHOP_ITEMS"
            );

        var existing = _items.FirstOrDefault(x =>
            x.OfferId == offerId
            && x.VariantId == variantId
            && x.SessionId == sessionId
            && x.UsageDate == usageDate
        );
        if (existing is not null)
            existing.UpdateQuantity(existing.Quantity + quantity);
        else
            _items.Add(
                CartItem.CreateFromOffer(
                    Id,
                    shopId,
                    productId,
                    offerId,
                    variantId,
                    sessionId,
                    usageDate,
                    quantity,
                    originalUnitPriceMinor,
                    finalUnitPriceMinor
                )
            );
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveItem(Guid itemId)
    {
        var item =
            _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        _items.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>ادغام دسته‌ای آیتم‌ها از سبد محلی — از AddItem موجود استفاده می‌کند تا dup-merge حفظ شود</summary>
    public void MergeItems(IReadOnlyList<MergeItemSpec> items)
    {
        foreach (var i in items)
            AddItem(
                i.ShopId,
                i.ProductId,
                i.VariantId,
                i.SessionId,
                i.UsageDate,
                i.Quantity,
                i.UnitPriceMinor
            );
    }

    public readonly record struct MergeItemSpec(
        Guid ShopId,
        Guid ProductId,
        Guid? VariantId,
        Guid? SessionId,
        DateOnly? UsageDate,
        int Quantity,
        long UnitPriceMinor
    );
}
