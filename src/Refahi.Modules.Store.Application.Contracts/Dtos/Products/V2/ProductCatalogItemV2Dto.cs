namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

public sealed record ProductCatalogItemV2Dto(
    Guid ProductId,
    string Title,
    string Slug,
    string? MainImageUrl,
    string ProductType,
    string DeliveryType,
    string SalesModel,
    int? CategoryId,
    string? CategoryName,
    string PriceDisplayMode,
    long MinEffectivePriceMinor,
    long MaxEffectivePriceMinor,
    long DefaultOriginalPriceMinor,
    long? DefaultDiscountedPriceMinor,
    int OfferCount,
    bool HasVariants,
    bool HasSessions,
    string DefaultOfferKey,
    Guid DefaultShopId,
    string DefaultShopSlug,
    DateTimeOffset CreatedAt);

