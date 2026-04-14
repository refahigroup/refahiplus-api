using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Product
{
    private Product()
    {
        _images = new List<ProductImage>();
        _variants = new List<ProductVariant>();
        _specifications = new List<ProductSpecification>();
        _sessions = new List<ProductSession>();
    }

    public Guid Id { get; private set; }
    public Guid ShopId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public long PriceMinor { get; private set; }                      // قیمت اصلی (ریال)
    public long? DiscountedPriceMinor { get; private set; }           // قیمت تخفیف‌خورده
    public int? DiscountPercent { get; private set; }                 // درصد تخفیف (نمایشی)
    public ProductType ProductType { get; private set; }
    public DeliveryType DeliveryType { get; private set; }
    public SalesModel SalesModel { get; private set; }                // v1.1
    public int StockCount { get; private set; }
    public bool IsAvailable { get; private set; }
    public string? City { get; private set; }
    public string? Area { get; private set; }                         // منطقه (ونک، عظیمیه، ...)
    public int CategoryId { get; private set; }                       // FK → store.categories
    public string CategoryCode { get; private set; } = string.Empty; // "store.clothing" (برای Wallet restriction)
    public bool IsDeleted { get; private set; }                       // Soft delete
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation
    private readonly List<ProductImage> _images;
    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

    private readonly List<ProductVariant> _variants;
    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    private readonly List<ProductSpecification> _specifications;
    public IReadOnlyList<ProductSpecification> Specifications => _specifications.AsReadOnly();

    private readonly List<ProductSession> _sessions;
    public IReadOnlyList<ProductSession> Sessions => _sessions.AsReadOnly();

    // --- Factory ---
    public static Product Create(
        Guid shopId, string title, string slug, long priceMinor,
        ProductType productType, DeliveryType deliveryType,
        SalesModel salesModel, int categoryId, string categoryCode,
        string? description = null, int stockCount = 0,
        string? city = null, string? area = null)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت محصول باید بیشتر از صفر باشد", "INVALID_PRICE");

        return new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLower(),
            Description = description,
            PriceMinor = priceMinor,
            ProductType = productType,
            DeliveryType = deliveryType,
            SalesModel = salesModel,
            StockCount = stockCount,
            IsAvailable = salesModel == SalesModel.StockBased ? stockCount > 0 : true,
            CategoryId = categoryId,
            CategoryCode = categoryCode,
            City = city,
            Area = area,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // --- StockBased Behaviors ---
    public void DecreaseStock(int quantity)
    {
        if (SalesModel != SalesModel.StockBased)
            throw new StoreDomainException("این عملیات فقط برای محصولات موجودی‌محور مجاز است", "INVALID_SALES_MODEL");
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (StockCount < quantity)
            throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
        StockCount -= quantity;
        IsAvailable = StockCount > 0;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void IncreaseStock(int quantity)
    {
        if (SalesModel != SalesModel.StockBased)
            throw new StoreDomainException("این عملیات فقط برای محصولات موجودی‌محور مجاز است", "INVALID_SALES_MODEL");
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        StockCount += quantity;
        IsAvailable = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // --- SessionBased Behavior ---
    public void AddSession(DateOnly date, TimeOnly startTime, TimeOnly endTime,
        int capacity, string? title = null, long priceAdjustment = 0)
    {
        if (SalesModel != SalesModel.SessionBased)
            throw new StoreDomainException("این عملیات فقط برای محصولات سانس‌محور مجاز است", "INVALID_SALES_MODEL");

        _sessions.Add(ProductSession.Create(Id, date, startTime, endTime, capacity, title, priceAdjustment));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // --- Common Behaviors ---
    public void UpdatePrice(long newPrice, long? discountedPrice = null, int? discountPercent = null)
    {
        if (newPrice <= 0)
            throw new StoreDomainException("قیمت باید بیشتر از صفر باشد", "INVALID_PRICE");
        PriceMinor = newPrice;
        DiscountedPriceMinor = discountedPrice;
        DiscountPercent = discountPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateInfo(string title, string? description, string? city, string? area, bool isAvailable)
    {
        Title = title.Trim();
        Description = description;
        City = city;
        Area = area;
        IsAvailable = isAvailable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsAvailable = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImage(string imageUrl, bool isMain = false, int sortOrder = 0)
    {
        _images.Add(ProductImage.Create(Id, imageUrl, isMain, sortOrder));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddVariant(string? size, string? color, string? colorHex,
        string? imageUrl, int stockCount, long priceAdjustment = 0)
    {
        _variants.Add(ProductVariant.Create(Id, size, color, colorHex, imageUrl, stockCount, priceAdjustment));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddSpecification(string key, string value, int sortOrder = 0)
    {
        _specifications.Add(ProductSpecification.Create(Id, key, value, sortOrder));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// قیمت موثر (با احتساب تخفیف)
    /// </summary>
    public long EffectivePriceMinor => DiscountedPriceMinor ?? PriceMinor;
}
