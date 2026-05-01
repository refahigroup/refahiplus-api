namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductDetailDto(
    Guid Id, Guid AgreementProductId, string Title, string Slug, string? Description,
    long PriceMinor, long DiscountedPriceMinor,
    string ProductType, string DeliveryType, string SalesModel,
    int? CategoryId, string? CategoryCode,
    bool IsAvailable, int StockCount,
    List<ProductImageDto> Images,
    List<ProductVariantDto> Variants,
    List<ProductSpecificationDto> Specifications,
    List<ProductSessionDto>? Sessions,
    double AverageRating, int ReviewCount,
    DateTimeOffset CreatedAt);
