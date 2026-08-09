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
    [Obsolete("فقط برای سازگاری APIهای legacy است؛ مسیر v3 از SupplierId و CategoryId استفاده می‌کند.")]
    public Guid AgreementProductId { get; private set; }             // FK → SupplyChain.AgreementProduct
    public Guid SupplierId { get; private set; }
    public int CategoryId { get; private set; }
    public ProductType ProductType { get; private set; }
    public SalesModel SalesModel { get; private set; }
    public FulfillmentMethod FulfillmentMethod { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int StockCount { get; private set; }
    public bool IsAvailable { get; private set; }
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
        Guid agreementProductId,
        string title,
        string slug,
        string? description = null,
        int stockCount = 0,
        bool? initialAvailability = null)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            AgreementProductId = agreementProductId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLower(),
            Description = description,
            StockCount = stockCount,
            IsAvailable = initialAvailability ?? stockCount > 0,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Product CreateCatalogProduct(
        Guid supplierId,
        int categoryId,
        ProductType productType,
        SalesModel salesModel,
        FulfillmentMethod fulfillmentMethod,
        string title,
        string slug,
        string? description = null)
    {
        if (supplierId == Guid.Empty)
            throw new StoreDomainException("شناسه تامین‌کننده الزامی است", "SUPPLIER_REQUIRED");
        if (categoryId <= 0)
            throw new StoreDomainException("دسته‌بندی محصول نامعتبر است", "INVALID_CATEGORY");
        if (!Enum.IsDefined(productType))
            throw new StoreDomainException("نوع محصول نامعتبر است", "INVALID_PRODUCT_TYPE");
        if (!Enum.IsDefined(salesModel))
            throw new StoreDomainException("مدل فروش محصول نامعتبر است", "INVALID_SALES_MODEL");
        if (!Enum.IsDefined(fulfillmentMethod))
            throw new StoreDomainException("روش تحویل محصول نامعتبر است", "INVALID_FULFILLMENT_METHOD");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug))
            throw new StoreDomainException("عنوان و اسلاگ محصول الزامی است", "INVALID_PRODUCT_CONTENT");

        var now = DateTimeOffset.UtcNow;
        return new Product
        {
            Id = Guid.NewGuid(),
            AgreementProductId = Guid.Empty,
            SupplierId = supplierId,
            CategoryId = categoryId,
            ProductType = productType,
            SalesModel = salesModel,
            FulfillmentMethod = fulfillmentMethod,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            StockCount = 0,
            IsAvailable = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateCatalogContent(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new StoreDomainException("عنوان محصول الزامی است", "PRODUCT_TITLE_REQUIRED");
        Title = title.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        if (IsDeleted)
            throw new StoreDomainException("محصول حذف‌شده قابل فعال‌سازی نیست", "PRODUCT_DELETED");
        IsAvailable = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        IsAvailable = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // --- StockBased Behaviors ---
    public void DecreaseStock(int quantity)
    {
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
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        StockCount += quantity;
        IsAvailable = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DecreaseVariantStock(Guid variantId, int quantity)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        variant.DecreaseStock(quantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // --- SessionBased Behavior ---
    public void AddSession(DateOnly date, TimeOnly startTime, TimeOnly endTime,
        int capacity, string? title = null, long priceAdjustment = 0)
    {
        _sessions.Add(ProductSession.Create(Id, date, startTime, endTime, capacity, title, priceAdjustment));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // --- Common Behaviors ---
    public void UpdateInfo(string title, string? description, bool isAvailable)
    {
        Title = title.Trim();
        Description = description;
        IsAvailable = isAvailable;
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

    public void RemoveImage(int imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new StoreDomainException("تصویر محصول یافت نشد", "IMAGE_NOT_FOUND");
        _images.Remove(image);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetMainImage(int imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new StoreDomainException("تصویر محصول یافت نشد", "IMAGE_NOT_FOUND");
        foreach (var img in _images)
            img.SetMain(img.Id == target.Id);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReorderImages(IEnumerable<(int ImageId, int SortOrder)> map)
    {
        var orderMap = map.ToDictionary(x => x.ImageId, x => x.SortOrder);
        foreach (var img in _images)
        {
            if (orderMap.TryGetValue(img.Id, out var order))
                img.SetSortOrder(order);
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Defines a new variant attribute (e.g. "سایز", "رنگ") with its initial values.
    /// Values can be empty and added later via AddVariantAttributeValue.
    /// </summary>
    public VariantAttribute AddVariantAttribute(string name, int sortOrder = 0)
    {
        var normalizedName = name.Trim();
        if (_variantAttributes.Any(a => string.Equals(a.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new StoreDomainException("ویژگی تنوع قبلاً برای این محصول ثبت شده است", "VARIANT_ATTRIBUTE_ALREADY_EXISTS");

        var attr = VariantAttribute.Create(Id, normalizedName, sortOrder);
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

    public void UpdateVariantAttribute(Guid attributeId, string name, int sortOrder = 0)
    {
        var attr = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
            ?? throw new StoreDomainException("ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");

        var normalizedName = name.Trim();
        if (_variantAttributes.Any(a => a.Id != attributeId &&
                                        string.Equals(a.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new StoreDomainException(
                "ویژگی تنوع قبلاً برای این محصول ثبت شده است",
                "VARIANT_ATTRIBUTE_ALREADY_EXISTS");
        }

        attr.Update(normalizedName, sortOrder);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateVariantAttributeValue(Guid attributeId, Guid valueId, string value, int sortOrder = 0)
    {
        var attr = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
            ?? throw new StoreDomainException("ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");

        attr.UpdateValue(valueId, value, sortOrder);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveVariantAttribute(Guid attributeId)
    {
        var attr = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
            ?? throw new StoreDomainException("ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");

        if (_variants.Any(v => v.Combinations.Any(c => c.VariantAttributeId == attributeId)))
            throw new StoreDomainException(
                "این ویژگی در تنوع‌های محصول استفاده شده است؛ ابتدا تنوع‌های وابسته را حذف کنید",
                "VARIANT_ATTRIBUTE_IN_USE");

        _variantAttributes.Remove(attr);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveVariantAttributeValue(Guid attributeId, Guid valueId)
    {
        var attr = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
            ?? throw new StoreDomainException("ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");

        if (_variants.Any(v => v.Combinations.Any(c => c.VariantAttributeValueId == valueId)))
            throw new StoreDomainException(
                "این مقدار در تنوع‌های محصول استفاده شده است؛ ابتدا تنوع‌های وابسته را حذف کنید",
                "VARIANT_ATTRIBUTE_VALUE_IN_USE");

        attr.RemoveValue(valueId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new ProductVariant from a set of (attributeId, valueId) combinations.
    /// All attributeIds must belong to this product; all valueIds must belong to the given attribute.
    /// </summary>
    public ProductVariant AddVariant(
        List<(Guid AttributeId, Guid ValueId)> combinations,
        int stockCount, long priceMinor, long? discountedPriceMinor = null,
        string? imageUrl = null, string? sku = null,
        DateOnly? fromDate = null, DateOnly? toDate = null,
        VariantCapacityType capacityType = VariantCapacityType.Unlimited, int? capacity = null,
        SalesModel salesModel = SalesModel.StockBased)
    {
        foreach (var (attrId, valueId) in combinations)
        {
            var attr = _variantAttributes.FirstOrDefault(a => a.Id == attrId)
                ?? throw new StoreDomainException("اتریبیوت تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");
            if (!attr.Values.Any(v => v.Id == valueId))
                throw new StoreDomainException("مقدار اتریبیوت به این اتریبیوت تعلق ندارد", "VARIANT_VALUE_NOT_FOUND");
        }

        var variant = ProductVariant.Create(
            Id,
            stockCount,
            priceMinor,
            discountedPriceMinor,
            imageUrl,
            sku,
            fromDate,
            toDate,
            capacityType,
            capacity,
            salesModel);
        foreach (var (attrId, valueId) in combinations)
            variant.AddCombination(attrId, valueId);

        _variants.Add(variant);
        UpdatedAt = DateTimeOffset.UtcNow;
        return variant;
    }

    public void UpdateVariant(
        Guid variantId,
        List<(Guid AttributeId, Guid ValueId)> combinations,
        int stockCount, long priceMinor, long? discountedPriceMinor = null,
        string? imageUrl = null, string? sku = null,
        DateOnly? fromDate = null, DateOnly? toDate = null,
        VariantCapacityType capacityType = VariantCapacityType.Unlimited, int? capacity = null,
        SalesModel salesModel = SalesModel.StockBased)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        foreach (var (attributeId, valueId) in combinations)
        {
            var attribute = _variantAttributes.FirstOrDefault(a => a.Id == attributeId)
                ?? throw new StoreDomainException("اتریبیوت تنوع یافت نشد", "VARIANT_ATTRIBUTE_NOT_FOUND");
            if (!attribute.Values.Any(v => v.Id == valueId))
                throw new StoreDomainException("مقدار اتریبیوت به این اتریبیوت تعلق ندارد", "VARIANT_VALUE_NOT_FOUND");
        }

        variant.Update(
            stockCount,
            priceMinor,
            discountedPriceMinor,
            imageUrl,
            sku,
            fromDate,
            toDate,
            capacityType,
            capacity,
            salesModel);
        variant.ReplaceCombinations(combinations);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveVariant(Guid variantId)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        _variants.Remove(variant);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddSpecification(string key, string value, int sortOrder = 0)
    {
        _specifications.Add(ProductSpecification.Create(Id, key, value, sortOrder));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
