using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Cart
{
    private Cart() { _items = new List<CartItem>(); }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<CartItem> _items;
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public long TotalMinor => _items.Sum(i => i.UnitPriceMinor * i.Quantity);

    // --- Factory ---
    public static Cart Create(Guid userId)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    // --- Behaviors ---
    public void AddItem(Guid productId, Guid? variantId, Guid? sessionId,
        int quantity, long unitPriceMinor)
    {
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (unitPriceMinor <= 0)
            throw new StoreDomainException("قیمت واحد باید بیشتر از صفر باشد", "INVALID_PRICE");

        var existing = _items.FirstOrDefault(i =>
            i.ProductId == productId &&
            i.VariantId == variantId &&
            i.SessionId == sessionId);

        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
        }
        else
        {
            _items.Add(CartItem.Create(Id, productId, variantId, sessionId, quantity, unitPriceMinor));
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateItemQuantity(Guid itemId, int newQuantity)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        item.UpdateQuantity(newQuantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        _items.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
