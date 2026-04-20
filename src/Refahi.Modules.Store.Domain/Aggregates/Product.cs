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
        _variantAttributes = new List<VariantAttribute>();
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
    public decimal CommissionPercent { get; private set; }            // درصد کمیسیون (کارمزد پلتفرم)
    public ProductType ProductType { get; private set; }
    public DeliveryType DeliveryType { get; private set; }
    public SalesModel SalesModel { get; private set; }                // v1.1
    public int StockCount { get; private set; }
    public bool IsAvailable { get; private set; }
    public int? CityId { get; private set; }                          // FK → References.City
    public string? City { get; private set; }                         // DEPRECATED — replaced by CityId
    public string? Area { get; private set; }                         // منطقه (ونک، عظیمیه، ...)
    public int CategoryId { get; private set; }                       // FK → store.categories
    public string CategoryCode { get; private set; } = string.Empty; // "store.clothing" (برای Wallet restriction)
    public bool IsDeleted { get; private set; }                       // Soft delete
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }                         // Optimistic concurrency (PostgreSQL xmin)

    // Navigation
    private readonly List<ProductImage> _images;
    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

    private readonly List<ProductVariant> _variants;
    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    private readonly List<VariantAttribute> _variantAttributes;
    public IReadOnlyList<VariantAttribute> VariantAttributes => _variantAttributes.AsReadOnly();

    private readonly List<ProductSpecification> _specifications;
    public IReadOnlyList<ProductSpecification> Specifications => _specifications.AsReadOnly();

    private readonly List<ProductSession> _sessions;
    public IReadOnlyList<ProductSession> Sessions => _sessions.AsReadOnly();

    // --- Factory ---
    public static Product Create(
        Guid shopId, string title, string slug, long priceMinor,
        ProductType productType, DeliveryType deliveryType,
        SalesModel salesModel, int categoryId, string categoryCode,
        decimal commissionPercent = 0,
        string? description = null, int stockCount = 0,
        int? cityId = null, string? city = null, string? area = null)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت محصول باید بیشتر از صفر باشد", "INVALID_PRICE");
        if (commissionPercent < 0 || commissionPercent > 100)
            throw new StoreDomainException("درصد کمیسیون باید بین 0 تا 100 باشد", "INVALID_COMMISSION");

        return new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLower(),
            Description = description,
            PriceMinor = priceMinor,
            CommissionPercent = commissionPercent,
            ProductType = productType,
            DeliveryType = deliveryType,
            SalesModel = salesModel,
            StockCount = stockCount,
            IsAvailable = salesModel == SalesModel.StockBased ? stockCount > 0 : true,
            CategoryId = categoryId,
            CategoryCode = categoryCode,
            CityId = cityId,
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

    public void DecreaseVariantStock(Guid variantId, int quantity)
    {
        if (SalesModel != SalesModel.StockBased)
            throw new StoreDomainException("این عملیات فقط برای محصولات موجودی‌محور مجاز است", "INVALID_SALES_MODEL");

        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        variant.DecreaseStock(quantity);
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

    public void UpdateInfo(string title, string? description, int? cityId, string? city, string? area, bool isAvailable)
    {
        Title = title.Trim();
        Description = description;
        CityId = cityId;
        City = city;
        Area = area;
        IsAvailable = isAvailable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateCommission(decimal commissionPercent)
    {
        if (commissionPercent < 0 || commissionPercent > 100)
            throw new StoreDomainException("درصد کمیسیون باید بین 0 تا 100 باشد", "INVALID_COMMISSION");
        CommissionPercent = commissionPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsAvailable = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImage(string imageUrl, bool isMain = false, int sortOrder = 0)
    {
        _images.Add(ProductImage.Create(Id, imageUrl, isMain, sortOrder));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Defines a new variant attribute (e.g. "سایز", "رنگ") with its initial values.
    /// Values can be empty and added later via AddVariantAttributeValue.
    /// </summary>
    public VariantAttribute AddVariantAttribute(string name, int sortOrder = 0)
    {
        var attr = VariantAttribute.Create(Id, name, sortOrder);
        _variantAttributes.Add(attr);
        UpdatedAt = DateTimeOffset.UtcNow;
        return attr;
    }

    /// <summary>
    /// Adds a value to an existing attribute of this product.
    /// </summary>
    public VariantAttributeValue AddVariantAttributeValue(Guid attributeId, string value, int sortOrder = 0)
    {
        var attr = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
            ?? throw new StoreDomainException("اتریبیوت تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");
        var attrValue = attr.AddValue(value, sortOrder);
        UpdatedAt = DateTimeOffset.UtcNow;
        return attrValue;
    }

    /// <summary>
    /// Creates a new ProductVariant from a set of (attributeId, valueId) combinations.
    /// All attributeIds must belong to this product; all valueIds must belong to the given attribute.
    /// </summary>
    public ProductVariant AddVariant(
        List<(Guid AttributeId, Guid ValueId)> combinations,
        int stockCount, long priceMinor, long? discountedPriceMinor = null,
        string? imageUrl = null, string? sku = null)
    {
        foreach (var (attrId, valueId) in combinations)
        {
            var attr = _variantAttributes.FirstOrDefault(a => a.Id == attrId)
                ?? throw new StoreDomainException("اتریبیوت تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");

            if (!attr.Values.Any(v => v.Id == valueId))
                throw new StoreDomainException("مقدار اتریبیوت به این اتریبیوت تعلق ندارد", "VARIANT_VALUE_NOT_FOUND");
        }

        var variant = ProductVariant.Create(Id, stockCount, priceMinor, discountedPriceMinor, imageUrl, sku);
        foreach (var (attrId, valueId) in combinations)
            variant.AddCombination(attrId, valueId);

        _variants.Add(variant);
        UpdatedAt = DateTimeOffset.UtcNow;
        return variant;
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
