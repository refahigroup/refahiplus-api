using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IPublicCatalogRepository
{
    Task<IReadOnlyList<PublicCatalogOfferCandidate>> GetEffectiveCandidatesAsync(
        int? moduleCategoryId, int? categoryId, Guid? shopId, string? shopSlug,
        string? search, SalesModel? salesModel, DateTimeOffset atUtc,
        CancellationToken ct = default);
}

public sealed record PublicCatalogOfferCandidate(
    Guid OfferId, Guid ProductId, Guid ShopId, Guid SupplierId, int CategoryId,
    string ProductTitle, string ProductSlug, string? ProductDescription,
    ProductType ProductType, SalesModel SalesModel, FulfillmentMethod FulfillmentMethod,
    DateTimeOffset ProductCreatedAt, string? MainImageUrl,
    string ShopName, string ShopSlug, long OriginalPriceMinor, decimal DiscountPercent,
    long FinalPriceMinor, Guid? ProductVariantId, Guid? ProductSessionId,
    DateTimeOffset StartDateUtc, DateTimeOffset? EndDateUtc,
    uint OfferVersion = 0, DateTimeOffset? OfferUpdatedAt = null);
