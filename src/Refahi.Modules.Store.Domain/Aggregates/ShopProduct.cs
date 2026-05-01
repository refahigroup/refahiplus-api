namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class ShopProduct
{
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
}
