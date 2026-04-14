namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductDetailDto(
    Guid Id, Guid ShopId, string Title, string Slug, string? Description,
    long PriceMinor, long? DiscountedPriceMinor, int? DiscountPercent,
    string ProductType, string DeliveryType, string SalesModel,
    int CategoryId, string CategoryCode,
    string? City, string? Area,
    bool IsAvailable, int StockCount,
    string ShopName, string ShopSlug,
    List<ProductImageDto> Images,
    List<ProductVariantDto> Variants,
    List<ProductSpecificationDto> Specifications,
    List<ProductSessionDto>? Sessions,
    double AverageRating, int ReviewCount,
    DateTimeOffset CreatedAt);
