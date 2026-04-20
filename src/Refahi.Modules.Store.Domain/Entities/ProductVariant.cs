using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class ProductVariant
{
    private ProductVariant() { _combinations = new List<ProductVariantCombination>(); }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string? SKU { get; private set; }            // اختیاری — کد موجودی
    public string? ImageUrl { get; private set; }
    public int StockCount { get; private set; }
    public long PriceMinor { get; private set; }        // قیمت مستقل (ریال)
    public long? DiscountedPriceMinor { get; private set; } // قیمت تخفیف‌خورده (ریال)
    public bool IsAvailable { get; private set; }

    private readonly List<ProductVariantCombination> _combinations;
    public IReadOnlyList<ProductVariantCombination> Combinations => _combinations.AsReadOnly();

    internal static ProductVariant Create(
        Guid productId, int stockCount, long priceMinor, long? discountedPriceMinor = null,
        string? imageUrl = null, string? sku = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SKU = sku,
            ImageUrl = imageUrl,
            StockCount = stockCount,
            PriceMinor = priceMinor,
            DiscountedPriceMinor = discountedPriceMinor,
            IsAvailable = stockCount > 0
        };

    public void UpdatePrice(long priceMinor, long? discountedPriceMinor = null)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت باید بیشتر از صفر باشد", "INVALID_PRICE");
        PriceMinor = priceMinor;
        DiscountedPriceMinor = discountedPriceMinor;
    }

    /// <summary>
    /// قیمت موثر (با احتساب تخفیف)
    /// </summary>
    public long EffectivePriceMinor => DiscountedPriceMinor ?? PriceMinor;

    internal void AddCombination(Guid attributeId, Guid valueId)
        => _combinations.Add(ProductVariantCombination.Create(Id, attributeId, valueId));

    internal void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (StockCount < quantity)
            throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
        StockCount -= quantity;
        IsAvailable = StockCount > 0;
    }

    internal void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        StockCount += quantity;
        IsAvailable = true;
    }
}
