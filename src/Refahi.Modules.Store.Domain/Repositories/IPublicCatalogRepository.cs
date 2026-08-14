using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IPublicCatalogRepository
{
    Task<IReadOnlyList<PublicCatalogEligibilityCoordinate>> GetEligibilityCoordinatesAsync(
        IReadOnlyCollection<int> categoryIds,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc,
        CancellationToken ct = default
    );

    Task<PublicCatalogCandidatePage> GetEffectivePageAsync(
        IReadOnlyCollection<int> categoryIds,
        IReadOnlyCollection<PublicCatalogEligibilityCoordinate> allowedCoordinates,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        long? minPriceMinor,
        long? maxPriceMinor,
        string sort,
        int page,
        int pageSize,
        DateTimeOffset atUtc,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<PublicCatalogOfferCandidate>> GetEffectiveCandidatesAsync(
        int? moduleCategoryId,
        int? categoryId,
        Guid? shopId,
        string? shopSlug,
        string? productSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc,
        CancellationToken ct = default
    );
}

public sealed record PublicCatalogEligibilityCoordinate(
    Guid SupplierId,
    int CategoryId,
    SalesChannel SalesChannel);

public sealed record PublicCatalogCandidatePage(
    IReadOnlyList<PublicCatalogOfferCandidate> Candidates,
    int Total
);

public sealed record PublicCatalogOfferCandidate(
    Guid OfferId,
    Guid ProductId,
    Guid ShopId,
    Guid SupplierId,
    int CategoryId,
    string ProductTitle,
    string ProductSlug,
    string? ProductDescription,
    ProductType ProductType,
    SalesModel SalesModel,
    FulfillmentMethod FulfillmentMethod,
    DateTimeOffset ProductCreatedAt,
    string? MainImageUrl,
    string ShopName,
    string ShopSlug,
    SalesChannel SalesChannel,
    long OriginalPriceMinor,
    decimal DiscountPercent,
    long FinalPriceMinor,
    Guid? ProductVariantId,
    Guid? ProductSessionId,
    DateTimeOffset StartDateUtc,
    DateTimeOffset? EndDateUtc,
    uint OfferVersion = 0,
    DateTimeOffset? OfferUpdatedAt = null
);
