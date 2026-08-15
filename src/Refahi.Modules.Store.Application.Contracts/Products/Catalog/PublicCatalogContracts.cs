using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Products;

public sealed record PublicOfferSelectionDto(
    Guid ShopId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId
);

public sealed record PublicOfferDto(
    Guid Id,
    Guid ProductId,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    Guid? ProductVariantId,
    Guid? ProductSessionId,
    long OriginalPriceMinor,
    decimal DiscountPercent,
    long FinalPriceMinor,
    DateTimeOffset StartDateUtc,
    DateTimeOffset? EndDateUtc,
    uint Version,
    DateTimeOffset UpdatedAt,
    bool IsAvailable,
    string AvailabilityCode,
    PublicOfferSelectionDto Selection
)
{
    public int? MaxQuantity { get; init; }
}

public sealed record PublicProductPriceSummaryDto(
    string DisplayMode,
    long MinFinalPriceMinor,
    long MaxFinalPriceMinor,
    int OfferCount,
    Guid DefaultOfferId,
    Guid DefaultShopId,
    string DefaultShopSlug,
    long DefaultOriginalPriceMinor,
    decimal DefaultDiscountPercent,
    long DefaultFinalPriceMinor
);

public sealed record PublicProductCatalogItemDto(
    Guid ProductId,
    string Title,
    string Slug,
    string? Description,
    string? MainImageUrl,
    short ProductType,
    short SalesModel,
    short FulfillmentMethod,
    int CategoryId,
    DateTimeOffset CreatedAt,
    bool HasVariants,
    bool HasSessions,
    PublicProductPriceSummaryDto Price
);

public sealed record PublicProductCatalogPage(
    IReadOnlyList<PublicProductCatalogItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

public sealed record GetPublicProductCatalogQuery(
    string ModuleSlug,
    string? Search,
    int? CategoryId,
    Guid? ShopId,
    string? ShopSlug,
    string? SalesModel,
    long? MinPriceMinor,
    long? MaxPriceMinor,
    string Sort,
    int Page,
    int PageSize
) : IRequest<PublicProductCatalogPage?>;

public sealed record PublicProductDetailDto(
    ProductDto Product,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<VariantAttributeDto> VariantAttributes,
    IReadOnlyList<ProductVariantStructureDto> Variants,
    IReadOnlyList<ProductSessionStructureDto> Sessions,
    IReadOnlyList<PublicOfferDto> Offers,
    PublicProductPriceSummaryDto Price,
    Guid SelectedOfferId,
    string PricingAuthority
);

public sealed record GetPublicProductDetailQuery(
    string ModuleSlug,
    string ProductSlug,
    Guid? ShopId = null,
    string? ShopSlug = null,
    Guid? OfferId = null,
    Guid? VariantId = null,
    Guid? SessionId = null
) : IRequest<PublicProductDetailDto?>;
