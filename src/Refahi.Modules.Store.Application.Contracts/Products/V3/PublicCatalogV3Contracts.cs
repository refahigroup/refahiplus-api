using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Products.V3;

public sealed record PublicOfferSelectionV3Dto(Guid ShopId, Guid ProductId,
    Guid? VariantId, Guid? SessionId);

public sealed record PublicOfferV3Dto(Guid Id, Guid ProductId, Guid ShopId,
    string ShopName, string ShopSlug, Guid? ProductVariantId, Guid? ProductSessionId,
    long OriginalPriceMinor, decimal DiscountPercent, long FinalPriceMinor,
    DateTimeOffset StartDateUtc, DateTimeOffset? EndDateUtc,
    uint Version, DateTimeOffset UpdatedAt, bool IsAvailable, string AvailabilityCode,
    PublicOfferSelectionV3Dto Selection);

public sealed record PublicProductPriceSummaryV3Dto(string DisplayMode,
    long MinFinalPriceMinor, long MaxFinalPriceMinor, int OfferCount,
    Guid DefaultOfferId, Guid DefaultShopId, string DefaultShopSlug,
    long DefaultOriginalPriceMinor, decimal DefaultDiscountPercent,
    long DefaultFinalPriceMinor);

public sealed record PublicProductCatalogItemV3Dto(Guid ProductId, string Title, string Slug,
    string? Description, string? MainImageUrl, short ProductType, short SalesModel,
    short FulfillmentMethod, int CategoryId, DateTimeOffset CreatedAt,
    bool HasVariants, bool HasSessions, PublicProductPriceSummaryV3Dto Price);

public sealed record PublicProductCatalogV3Page(IReadOnlyList<PublicProductCatalogItemV3Dto> Items,
    int Total, int Page, int PageSize);

public sealed record GetPublicProductCatalogV3Query(string ModuleSlug, string? Search,
    int? CategoryId, Guid? ShopId, string? ShopSlug, string? SalesModel,
    long? MinPriceMinor, long? MaxPriceMinor, string Sort, int Page, int PageSize)
    : IRequest<PublicProductCatalogV3Page?>;

public sealed record PublicProductDetailV3Dto(ProductV3Dto Product,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<VariantAttributeDto> VariantAttributes,
    IReadOnlyList<ProductVariantV3Dto> Variants,
    IReadOnlyList<ProductSessionV3Dto> Sessions,
    IReadOnlyList<PublicOfferV3Dto> Offers,
    PublicProductPriceSummaryV3Dto Price,
    Guid SelectedOfferId,
    string PricingAuthority);

public sealed record GetPublicProductDetailV3Query(string ModuleSlug, string ProductSlug,
    Guid? ShopId = null, string? ShopSlug = null, Guid? OfferId = null,
    Guid? VariantId = null, Guid? SessionId = null)
    : IRequest<PublicProductDetailV3Dto?>;
