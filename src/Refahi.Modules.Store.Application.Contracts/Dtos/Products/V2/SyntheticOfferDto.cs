namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

public sealed record SyntheticOfferDto(
    string OfferKey,
    string OfferKind,
    Guid ProductId,
    string ProductTitle,
    string ProductSlug,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    Guid? VariantId,
    string? VariantLabel,
    Guid? SessionId,
    DateOnly? SessionDate,
    string? SessionStartTime,
    string? SessionEndTime,
    string? SessionTitle,
    long OriginalPriceMinor,
    long? DiscountedPriceMinor,
    long EffectivePriceMinor,
    int? DiscountPercent,
    int? AvailableStock,
    int? ConfiguredCapacity,
    bool RequiresUsageDate,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string AvailabilityCheck,
    string ProductType,
    string DeliveryType,
    string SalesModel,
    int? CategoryId,
    string? CategoryName,
    string? MainImageUrl,
    SyntheticOfferPurchaseSelectionDto PurchaseSelection);

public sealed record SyntheticOfferPurchaseSelectionDto(
    Guid ShopId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId,
    DateOnly? UsageDate);

