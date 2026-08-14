using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class PublicCatalogRepository(StoreDbContext db) : IPublicCatalogRepository
{
    public async Task<IReadOnlyList<PublicCatalogEligibilityCoordinate>> GetEligibilityCoordinatesAsync(
        IReadOnlyCollection<int> categoryIds,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc,
        CancellationToken ct = default)
    {
        var query = BuildEligibilityCoordinatesQuery(
            categoryIds,
            shopId,
            shopSlug,
            search,
            salesModel,
            atUtc);
        return await query.Distinct().ToListAsync(ct);
    }

    internal IQueryable<PublicCatalogEligibilityCoordinate> BuildEligibilityCoordinatesQuery(
        IReadOnlyCollection<int> categoryIds,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc)
    {
        var offers = BuildEffectiveOffers(atUtc, search);
        var products = BuildEffectiveProducts(categoryIds, null, salesModel);
        var shops = BuildEffectiveShops(shopId, shopSlug);
        return
            from offer in offers
            join product in products
                on new { offer.ProductId, offer.SupplierId }
                equals new { ProductId = product.Id, product.SupplierId }
            join shop in shops
                on new { offer.ShopId, offer.SupplierId }
                equals new { ShopId = shop.Id, shop.SupplierId }
            select new PublicCatalogEligibilityCoordinate(product.SupplierId, product.CategoryId);
    }

    public async Task<PublicCatalogCandidatePage> GetEffectivePageAsync(
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
        CancellationToken ct = default)
    {
        if (allowedCoordinates.Count == 0)
            return new([], 0);

        var offers = BuildEffectiveOffers(atUtc, search);
        var products = ApplyEligibility(
            BuildEffectiveProducts(categoryIds, null, salesModel),
            allowedCoordinates);
        var shops = BuildEffectiveShops(shopId, shopSlug);
        if (minPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor >= minPriceMinor.Value);
        if (maxPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor <= maxPriceMinor.Value);

        var rows =
            from offer in offers
            join product in products
                on new { offer.ProductId, offer.SupplierId }
                equals new { ProductId = product.Id, product.SupplierId }
            join shop in shops
                on new { offer.ShopId, offer.SupplierId }
                equals new { ShopId = shop.Id, shop.SupplierId }
            select new { Offer = offer, Product = product };
        var productPage = rows
            .GroupBy(x => new { x.Product.Id, x.Product.CreatedAt })
            .Select(x => new
            {
                ProductId = x.Key.Id,
                x.Key.CreatedAt,
                MinPrice = x.Min(y => y.Offer.FinalPriceMinor)
            });

        var total = await productPage.CountAsync(ct);
        var ordered = sort switch
        {
            "price-asc" => productPage.OrderBy(x => x.MinPrice).ThenBy(x => x.ProductId),
            "price-desc" => productPage.OrderByDescending(x => x.MinPrice).ThenBy(x => x.ProductId),
            _ => productPage.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ProductId)
        };
        var productIds = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.ProductId)
            .ToListAsync(ct);
        if (productIds.Count == 0)
            return new([], total);

        products = products.Where(x => productIds.Contains(x.Id));
        var candidates = await BuildCandidateQuery(offers, products, shops).ToListAsync(ct);
        return new(candidates, total);
    }

    public Task<IReadOnlyList<PublicCatalogOfferCandidate>> GetEffectiveCandidatesAsync(
        int? moduleCategoryId,
        int? categoryId,
        Guid? shopId,
        string? shopSlug,
        string? productSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc,
        CancellationToken ct = default)
    {
        IReadOnlyCollection<int> categories = categoryId.HasValue
            ? [categoryId.Value]
            : moduleCategoryId.HasValue ? [moduleCategoryId.Value] : [];
        var offers = BuildEffectiveOffers(atUtc, search);
        var products = BuildEffectiveProducts(categories, productSlug, salesModel);
        var shops = BuildEffectiveShops(shopId, shopSlug);
        return ReadAsync(BuildCandidateQuery(offers, products, shops), ct);
    }

    private IQueryable<Domain.Aggregates.Offer> BuildEffectiveOffers(
        DateTimeOffset atUtc,
        string? search)
    {
        var query = db.Offers.AsNoTracking()
            .Where(offer => offer.IsActive && !offer.IsDeleted
                && offer.StartDateUtc <= atUtc
                && (!offer.EndDateUtc.HasValue || atUtc < offer.EndDateUtc.Value));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            var matchingProductIds = db.Products
                .Where(x => x.Title.Contains(value)
                    || (x.Description != null && x.Description.Contains(value)))
                .Select(x => x.Id);
            var matchingShopIds = db.Shops
                .Where(x => x.Name.Contains(value))
                .Select(x => x.Id);
            query = query.Where(x => matchingProductIds.Contains(x.ProductId)
                || matchingShopIds.Contains(x.ShopId));
        }
        return query;
    }

    private IQueryable<Domain.Aggregates.Product> BuildEffectiveProducts(
        IReadOnlyCollection<int> categoryIds,
        string? productSlug,
        SalesModel? salesModel)
    {
        var query = db.Products.AsNoTracking()
            .Where(x => x.IsAvailable && !x.IsDeleted && x.SupplierId != Guid.Empty);
        if (categoryIds.Count > 0)
            query = query.Where(x => categoryIds.Contains(x.CategoryId));
        if (!string.IsNullOrWhiteSpace(productSlug))
        {
            var value = productSlug.Trim().ToLowerInvariant();
            query = query.Where(x => x.Slug == value);
        }
        if (salesModel.HasValue)
            query = query.Where(x => x.SalesModel == salesModel.Value);
        return query;
    }

    private IQueryable<Domain.Aggregates.Shop> BuildEffectiveShops(Guid? shopId, string? shopSlug)
    {
        var query = db.Shops.AsNoTracking()
            .Where(x => x.Status == ShopStatus.Active && x.ShopType == ShopType.Online);
        if (shopId.HasValue)
            query = query.Where(x => x.Id == shopId.Value);
        if (!string.IsNullOrWhiteSpace(shopSlug))
        {
            var value = shopSlug.Trim().ToLowerInvariant();
            query = query.Where(x => x.Slug == value);
        }
        return query;
    }

    private static IQueryable<Domain.Aggregates.Product> ApplyEligibility(
        IQueryable<Domain.Aggregates.Product> products,
        IReadOnlyCollection<PublicCatalogEligibilityCoordinate> coordinates)
    {
        IQueryable<Domain.Aggregates.Product>? eligibleProducts = null;
        foreach (var group in coordinates.GroupBy(x => x.CategoryId))
        {
            var categoryId = group.Key;
            var supplierIds = group.Select(x => x.SupplierId).Distinct().ToArray();
            var groupProducts = products.Where(x => x.CategoryId == categoryId
                && supplierIds.Contains(x.SupplierId));
            eligibleProducts = eligibleProducts is null
                ? groupProducts
                : eligibleProducts.Concat(groupProducts);
        }
        return eligibleProducts!;
    }

    private static IQueryable<PublicCatalogOfferCandidate> BuildCandidateQuery(
        IQueryable<Domain.Aggregates.Offer> offers,
        IQueryable<Domain.Aggregates.Product> products,
        IQueryable<Domain.Aggregates.Shop> shops) =>
        from offer in offers
        join product in products
            on new { offer.ProductId, offer.SupplierId }
            equals new { ProductId = product.Id, product.SupplierId }
        join shop in shops
            on new { offer.ShopId, offer.SupplierId }
            equals new { ShopId = shop.Id, shop.SupplierId }
        orderby product.CreatedAt descending, product.Id, offer.FinalPriceMinor, offer.Id
        select new PublicCatalogOfferCandidate(
                offer.Id, product.Id, shop.Id, product.SupplierId, product.CategoryId,
                product.Title, product.Slug, product.Description, product.ProductType,
                product.SalesModel, product.FulfillmentMethod, product.CreatedAt,
                product.Images.Where(i => i.IsMain).OrderBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl).FirstOrDefault()
                    ?? product.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                shop.Name, shop.Slug, offer.OriginalPriceMinor, offer.DiscountPercent,
                offer.FinalPriceMinor, offer.ProductVariantId, offer.ProductSessionId,
                offer.StartDateUtc, offer.EndDateUtc, offer.Version, offer.UpdatedAt);

    private static async Task<IReadOnlyList<PublicCatalogOfferCandidate>> ReadAsync(
        IQueryable<PublicCatalogOfferCandidate> query, CancellationToken ct) =>
        await query.ToListAsync(ct);
}
