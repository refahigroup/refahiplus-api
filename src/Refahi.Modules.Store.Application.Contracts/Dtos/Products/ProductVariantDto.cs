namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductVariantDto(
    Guid Id,
    string? Sku,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsAvailable,
    List<VariantCombinationDto> Combinations);

public sealed record VariantCombinationDto(
    Guid AttributeId,
    string AttributeName,
    Guid ValueId,
    string Value);
