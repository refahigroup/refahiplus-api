using Refahi.Modules.Store.Domain.Enums;
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
    public DateOnly? FromDate { get; private set; }
    public DateOnly? ToDate { get; private set; }
    public VariantCapacityType CapacityType { get; private set; }
    public int? Capacity { get; private set; }
    public bool IsAvailable { get; private set; }

    public bool RequiresUsageDate =>
        CapacityType == VariantCapacityType.PerEligibleDay &&
        FromDate.HasValue &&
        ToDate.HasValue &&
        FromDate.Value != ToDate.Value;

    public bool UsesLegacyStockFor(SalesModel salesModel)
        => salesModel == SalesModel.StockBased;

    public bool UsesAccessCapacityFor(SalesModel salesModel)
        => salesModel == SalesModel.SessionBased;

    public bool HasLegacyStockAvailable(int requestedQuantity)
        => IsAvailable && StockCount >= requestedQuantity;

    public bool IsAvailableFor(SalesModel salesModel)
        => salesModel switch
        {
            SalesModel.StockBased => IsAvailable,
            SalesModel.SessionBased => CapacityType == VariantCapacityType.Unlimited || Capacity is > 0,
            _ => IsAvailable
        };

    private readonly List<ProductVariantCombination> _combinations;
    public IReadOnlyList<ProductVariantCombination> Combinations => _combinations.AsReadOnly();

    internal static ProductVariant Create(
        Guid productId, int stockCount, long priceMinor, long? discountedPriceMinor = null,
        string? imageUrl = null, string? sku = null,
        DateOnly? fromDate = null, DateOnly? toDate = null,
        VariantCapacityType capacityType = VariantCapacityType.Unlimited, int? capacity = null,
        SalesModel salesModel = SalesModel.StockBased)
    {
        ValidatePrice(priceMinor, discountedPriceMinor);
        ValidateValidityRange(fromDate, toDate);
        var normalizedCapacity = ValidateAndNormalizeCapacity(capacityType, capacity);

        return new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SKU = sku,
            ImageUrl = imageUrl,
            StockCount = stockCount,
            PriceMinor = priceMinor,
            DiscountedPriceMinor = discountedPriceMinor,
            FromDate = fromDate,
            ToDate = toDate,
            CapacityType = capacityType,
            Capacity = normalizedCapacity,
            IsAvailable = DetermineInitialAvailability(salesModel, stockCount)
        };
    }

    public void UpdatePrice(long priceMinor, long? discountedPriceMinor = null)
    {
        ValidatePrice(priceMinor, discountedPriceMinor);
        PriceMinor = priceMinor;
        DiscountedPriceMinor = discountedPriceMinor;
    }

    /// <summary>
    /// قیمت موثر (با احتساب تخفیف)
    /// </summary>
    public long EffectivePriceMinor => DiscountedPriceMinor ?? PriceMinor;

    public void ValidateOrderEligibility(DateOnly? usageDate = null)
    {
        if (!IsAvailable)
            throw new StoreDomainException("تنوع محصول فعال نیست", "VARIANT_NOT_AVAILABLE");

        if (RequiresUsageDate && usageDate is null)
            throw new StoreDomainException("انتخاب تاریخ استفاده برای این تنوع الزامی است", "USAGE_DATE_REQUIRED");

        if (usageDate.HasValue && FromDate.HasValue && ToDate.HasValue &&
            (usageDate.Value < FromDate.Value || usageDate.Value > ToDate.Value))
            throw new StoreDomainException("تاریخ استفاده خارج از بازه اعتبار تنوع است", "USAGE_DATE_OUT_OF_RANGE");
    }

    public void EnsureCapacityAvailable(int requestedQuantity, int soldCountInScope)
    {
        if (requestedQuantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");

        if (soldCountInScope < 0)
            throw new StoreDomainException("تعداد فروخته‌شده نامعتبر است", "INVALID_SOLD_COUNT");

        if (CapacityType == VariantCapacityType.Unlimited)
            return;

        if (Capacity is null or <= 0)
            throw new StoreDomainException("ظرفیت تنوع معتبر نیست", "INVALID_VARIANT_CAPACITY");

        // TODO: Enforce variant capacity atomically in checkout/payment to avoid oversell.
        // TODO: Optimize runtime sold-count calculation via projection/cache/ledger before high-volume use.
        if (soldCountInScope + requestedQuantity > Capacity.Value)
            throw new StoreDomainException("ظرفیت تنوع کافی نیست", "INSUFFICIENT_VARIANT_CAPACITY");
    }

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

    private static void ValidatePrice(long priceMinor, long? discountedPriceMinor)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت باید بیشتر از صفر باشد", "INVALID_PRICE");

        if (discountedPriceMinor is <= 0)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید بیشتر از صفر باشد", "INVALID_DISCOUNTED_PRICE");

        if (discountedPriceMinor >= priceMinor)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید کمتر از قیمت اصلی باشد", "INVALID_DISCOUNTED_PRICE");
    }

    private static void ValidateValidityRange(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue != toDate.HasValue)
            throw new StoreDomainException("تاریخ شروع و پایان اعتبار باید همزمان ثبت شوند", "INVALID_VARIANT_VALIDITY_RANGE");

        if (fromDate.HasValue && fromDate.Value > toDate!.Value)
            throw new StoreDomainException("تاریخ شروع اعتبار باید قبل از تاریخ پایان باشد", "INVALID_VARIANT_VALIDITY_RANGE");
    }

    private static int? ValidateAndNormalizeCapacity(VariantCapacityType capacityType, int? capacity)
    {
        return capacityType switch
        {
            VariantCapacityType.Unlimited => null,
            VariantCapacityType.TotalPeriod or VariantCapacityType.PerEligibleDay when capacity is > 0 => capacity,
            VariantCapacityType.TotalPeriod or VariantCapacityType.PerEligibleDay =>
                throw new StoreDomainException("ظرفیت تنوع باید بیشتر از صفر باشد", "INVALID_VARIANT_CAPACITY"),
            _ => throw new StoreDomainException("نوع ظرفیت تنوع معتبر نیست", "INVALID_VARIANT_CAPACITY_TYPE")
        };
    }

    private static bool DetermineInitialAvailability(SalesModel salesModel, int stockCount)
        => salesModel switch
        {
            SalesModel.StockBased => stockCount > 0,
            SalesModel.SessionBased => true,
            _ => stockCount > 0
        };
}
