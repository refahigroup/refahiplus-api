namespace Refahi.Modules.Store.Domain.Entities;

public sealed class ProductVariant
{
    private ProductVariant() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string? Size { get; private set; }           // "S", "M", "L", "XL", "XXL"
    public string? Color { get; private set; }          // "ملانژ", "مشکی"
    public string? ColorHex { get; private set; }       // "#333333"
    public string? ImageUrl { get; private set; }
    public int StockCount { get; private set; }
    public long PriceAdjustment { get; private set; }   // تفاوت قیمت (معمولاً 0)
    public bool IsAvailable { get; private set; }

    internal static ProductVariant Create(
        Guid productId, string? size, string? color, string? colorHex,
        string? imageUrl, int stockCount, long priceAdjustment)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Size = size,
            Color = color,
            ColorHex = colorHex,
            ImageUrl = imageUrl,
            StockCount = stockCount,
            PriceAdjustment = priceAdjustment,
            IsAvailable = stockCount > 0
        };
}
