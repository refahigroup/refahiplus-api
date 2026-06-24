namespace Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;

public sealed record ShopProductVariantDto(
    Guid Id,
    Guid ShopProductId,
    Guid ProductVariantId,
    string VariantName,
    long ProductVariantPriceMinor,
    long? ProductVariantDiscountedPriceMinor,
    int ProductVariantStockCount,
    int? ProductVariantCapacity,
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive,
    bool IsDeleted);
