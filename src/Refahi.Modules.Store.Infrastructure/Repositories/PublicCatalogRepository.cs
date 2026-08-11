using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class PublicCatalogRepository(StoreDbContext db) : IPublicCatalogRepository
{
    public async Task<IReadOnlyList<PublicCatalogOfferCandidate>> GetEffectiveCandidatesAsync(
    int? moduleCategoryId,
    int? categoryId,
    Guid? shopId,
    string? shopSlug,
    string? productSlug,
    string? search,
    SalesModel? salesModel,
    DateTimeOffset atUtc,
    CancellationToken ct = default
)
    {
        var query =
            from offer in db.Offers.AsNoTracking()
            join product in db.Products.AsNoTracking() on offer.ProductId equals product.Id
            join shop in db.Shops.AsNoTracking() on offer.ShopId equals shop.Id
            where
                offer.IsActive
                && !offer.IsDeleted
                && offer.StartDateUtc <= atUtc
                && (!offer.EndDateUtc.HasValue || atUtc < offer.EndDateUtc.Value)
                && product.IsAvailable
                && !product.IsDeleted
                && product.SupplierId != Guid.Empty
                && product.SupplierId == shop.SupplierId
                && shop.Status == ShopStatus.Active
                && shop.ShopType == ShopType.Online
            select new
            {
                Offer = offer,
                Product = product,
                Shop = shop,
            };

        if (moduleCategoryId.HasValue)
            query = query.Where(x => x.Product.CategoryId == moduleCategoryId.Value);

        if (categoryId.HasValue)
            query = query.Where(x => x.Product.CategoryId == categoryId.Value);

        if (shopId.HasValue)
            query = query.Where(x => x.Shop.Id == shopId.Value);

        if (!string.IsNullOrWhiteSpace(shopSlug))
        {
            var normalizedShopSlug = shopSlug.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Shop.Slug == normalizedShopSlug
            );
        }

        // مهم:
        // ProductSlug قبل از ToListAsync و مستقیماً در SQL فیلتر می‌شود.
        // بنابراین در صفحه Detail دیگر کل Catalog از دیتابیس خوانده نمی‌شود.
        if (!string.IsNullOrWhiteSpace(productSlug))
        {
            var normalizedProductSlug = productSlug.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Product.Slug == normalizedProductSlug
            );
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Product.Title.Contains(term)
                || (x.Product.Description != null && x.Product.Description.Contains(term))
                || x.Shop.Name.Contains(term)
            );
        }

        if (salesModel.HasValue)
            query = query.Where(x =>
                x.Product.SalesModel == salesModel.Value
            );

        return await query
            .OrderByDescending(x => x.Product.CreatedAt)
            .ThenBy(x => x.Product.Id)
            .ThenBy(x => x.Offer.FinalPriceMinor)
            .ThenBy(x => x.Offer.Id)
            .Select(x => new PublicCatalogOfferCandidate(
                x.Offer.Id,
                x.Product.Id,
                x.Shop.Id,
                x.Product.SupplierId,
                x.Product.CategoryId,
                x.Product.Title,
                x.Product.Slug,
                x.Product.Description,
                x.Product.ProductType,
                x.Product.SalesModel,
                x.Product.FulfillmentMethod,
                x.Product.CreatedAt,
                x.Product.Images
                    .Where(image => image.IsMain)
                    .OrderBy(image => image.SortOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault()
                    ?? x.Product.Images
                        .OrderBy(image => image.SortOrder)
                        .Select(image => image.ImageUrl)
                        .FirstOrDefault(),
                x.Shop.Name,
                x.Shop.Slug,
                x.Offer.OriginalPriceMinor,
                x.Offer.DiscountPercent,
                x.Offer.FinalPriceMinor,
                x.Offer.ProductVariantId,
                x.Offer.ProductSessionId,
                x.Offer.StartDateUtc,
                x.Offer.EndDateUtc,
                x.Offer.Version,
                x.Offer.UpdatedAt
            ))
            .ToListAsync(ct);
    }
}
