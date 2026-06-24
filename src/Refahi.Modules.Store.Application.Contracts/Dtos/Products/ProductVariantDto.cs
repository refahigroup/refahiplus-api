using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductVariantDto(
    Guid Id,
    string? Sku,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    DateOnly? FromDate,
    DateOnly? ToDate,
    VariantCapacityType CapacityType,
    int? Capacity,
    bool RequiresUsageDate,
    bool IsAvailable,
    List<VariantCombinationDto> Combinations,
    Guid? ShopProductVariantId = null,
    long? ShopPriceMinor = null,
    long? ShopDiscountedPriceMinor = null,
    string? PriceSource = null,
    bool IsActiveInShop = true,
    bool UsesShopSpecificPrice = false);

public sealed record VariantCombinationDto(
    Guid AttributeId,
    string AttributeName,
    Guid ValueId,
    string Value);
