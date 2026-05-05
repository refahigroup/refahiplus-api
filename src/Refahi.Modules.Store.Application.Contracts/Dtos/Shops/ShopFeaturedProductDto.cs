namespace Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

public sealed record ShopFeaturedProductDto(
    Guid ProductId,
    string Title,
    string Slug,
    string? ImageUrl,
    long PriceMinor,
    long? DiscountedPriceMinor);
