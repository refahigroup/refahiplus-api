namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductOfferingSummaryDto(
    Guid ProductId,
    Guid ProductVariantId,
    Guid ShopProductVariantId,
    Guid ShopId,
    string Title,
    string Slug,
    string VariantLabel,
    string ShopName,
    string ShopSlug,
    long PriceMinor,
    long? DiscountedPriceMinor,
    int? DiscountPercent,
    string ProductType,
    string DeliveryType,
    string SalesModel,
    string? MainImageUrl,
    bool IsAvailable);
