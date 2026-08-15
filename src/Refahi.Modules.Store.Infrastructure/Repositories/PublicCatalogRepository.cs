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
        var shops = BuildEffectiveShops(shopId, shopSlug, includeInPerson: true);
        return
            from offer in offers
            join product in products
                on new { offer.ProductId, offer.SupplierId }
                equals new { ProductId = product.Id, product.SupplierId }
            join shop in shops
                on new { offer.ShopId, offer.SupplierId }
                equals new { ShopId = shop.Id, shop.SupplierId }
            select new PublicCatalogEligibilityCoordinate(
                product.SupplierId,
                product.CategoryId,
                shop.ShopType == ShopType.InPerson
                    ? SalesChannel.InPerson
                    : SalesChannel.Online);
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

        var productIdsQuery = BuildEligibleProductIdsQuery(
            categoryIds,
            allowedCoordinates,
            shopId,
            shopSlug,
            search,
            salesModel,
            minPriceMinor,
            maxPriceMinor,
            atUtc,
            sort);

        var total = await productIdsQuery.CountAsync(ct);
        var productIds = await productIdsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        if (productIds.Count == 0)
            return new([], total);

        var candidates = BuildEligibleCandidatesQuery(
            categoryIds,
            allowedCoordinates,
            shopId,
            shopSlug,
            search,
            salesModel,
            minPriceMinor,
            maxPriceMinor,
            atUtc,
            productIds);
        var pageCandidates = await candidates.ToListAsync(ct);
        var imageRows = await db.ProductImages.AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderByDescending(x => x.IsMain)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new { x.ProductId, x.ImageUrl })
            .ToListAsync(ct);
        var mainImageByProductId = imageRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First().ImageUrl);
        var hydratedCandidates = pageCandidates
            .Select(x => x with
            {
                MainImageUrl = mainImageByProductId.GetValueOrDefault(x.ProductId)
            })
            .ToArray();
        return new(hydratedCandidates, total);
    }

    internal IQueryable<Guid> BuildEligibleProductIdsQuery(
        IReadOnlyCollection<int> categoryIds,
        IReadOnlyCollection<PublicCatalogEligibilityCoordinate> allowedCoordinates,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        long? minPriceMinor,
        long? maxPriceMinor,
        DateTimeOffset atUtc,
        string sort)
    {
        var offers = BuildEffectiveOffers(atUtc, search);
        if (minPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor >= minPriceMinor.Value);
        if (maxPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor <= maxPriceMinor.Value);

        var products = BuildEffectiveProducts(categoryIds, null, salesModel);
        var shops = BuildEffectiveShops(shopId, shopSlug, includeInPerson: true);
        var eligibleRows =
            from offer in offers
            join product in products
                on new { offer.ProductId, offer.SupplierId }
                equals new { ProductId = product.Id, product.SupplierId }
            join shop in shops
                on new { offer.ShopId, offer.SupplierId }
                equals new { ShopId = shop.Id, shop.SupplierId }
            where false
            select new { Offer = offer, Product = product, Shop = shop };

        foreach (var group in allowedCoordinates.GroupBy(x => new { x.CategoryId, x.SalesChannel }))
        {
            var categoryId = group.Key.CategoryId;
            var shopType = group.Key.SalesChannel == SalesChannel.InPerson
                ? ShopType.InPerson
                : ShopType.Online;
            var supplierIds = group.Select(x => x.SupplierId).Distinct().ToArray();
            var eligibleProducts = products.Where(x => x.CategoryId == categoryId
                && supplierIds.Contains(x.SupplierId));
            var eligibleShops = shops.Where(x => x.ShopType == shopType
                && supplierIds.Contains(x.SupplierId));
            var channelRows =
                from offer in offers
                join product in eligibleProducts
                    on new { offer.ProductId, offer.SupplierId }
                    equals new { ProductId = product.Id, product.SupplierId }
                join shop in eligibleShops
                    on new { offer.ShopId, offer.SupplierId }
                    equals new { ShopId = shop.Id, shop.SupplierId }
                select new { Offer = offer, Product = product, Shop = shop };
            eligibleRows = eligibleRows.Concat(channelRows);
        }

        var productPage = eligibleRows
            .GroupBy(x => new { x.Product.Id, x.Product.CreatedAt })
            .Select(x => new
            {
                ProductId = x.Key.Id,
                x.Key.CreatedAt,
                MinPrice = x.Min(y => y.Offer.FinalPriceMinor)
            });

        return sort switch
        {
            "price-asc" => productPage
                .OrderBy(x => x.MinPrice)
                .ThenBy(x => x.ProductId)
                .Select(x => x.ProductId),
            "price-desc" => productPage
                .OrderByDescending(x => x.MinPrice)
                .ThenBy(x => x.ProductId)
                .Select(x => x.ProductId),
            _ => productPage
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.ProductId)
                .Select(x => x.ProductId)
        };
    }

    internal IQueryable<PublicCatalogOfferCandidate> BuildEligibleCandidatesQuery(
        IReadOnlyCollection<int> categoryIds,
        IReadOnlyCollection<PublicCatalogEligibilityCoordinate> allowedCoordinates,
        Guid? shopId,
        string? shopSlug,
        string? search,
        SalesModel? salesModel,
        long? minPriceMinor,
        long? maxPriceMinor,
        DateTimeOffset atUtc,
        IReadOnlyCollection<Guid>? productIds = null)
    {
        var offers = BuildEffectiveOffers(atUtc, search);
        if (minPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor >= minPriceMinor.Value);
        if (maxPriceMinor.HasValue)
            offers = offers.Where(x => x.FinalPriceMinor <= maxPriceMinor.Value);

        var products = BuildEffectiveProducts(categoryIds, null, salesModel);
        var shops = BuildEffectiveShops(shopId, shopSlug, includeInPerson: true);
        var eligibleRows =
            from offer in offers
            join product in products
                on new { offer.ProductId, offer.SupplierId }
                equals new { ProductId = product.Id, product.SupplierId }
            join shop in shops
                on new { offer.ShopId, offer.SupplierId }
                equals new { ShopId = shop.Id, shop.SupplierId }
            where false
            select new { Offer = offer, Product = product, Shop = shop };

        foreach (var group in allowedCoordinates.GroupBy(x => new { x.CategoryId, x.SalesChannel }))
        {
            var categoryId = group.Key.CategoryId;
            var shopType = group.Key.SalesChannel == SalesChannel.InPerson
                ? ShopType.InPerson
                : ShopType.Online;
            var supplierIds = group.Select(x => x.SupplierId).Distinct().ToArray();
            var eligibleProducts = products.Where(x => x.CategoryId == categoryId
                && supplierIds.Contains(x.SupplierId));
            var eligibleShops = shops.Where(x => x.ShopType == shopType
                && supplierIds.Contains(x.SupplierId));
            var channelRows =
                from offer in offers
                join product in eligibleProducts
                    on new { offer.ProductId, offer.SupplierId }
                    equals new { ProductId = product.Id, product.SupplierId }
                join shop in eligibleShops
                    on new { offer.ShopId, offer.SupplierId }
                    equals new { ShopId = shop.Id, shop.SupplierId }
                select new { Offer = offer, Product = product, Shop = shop };
            eligibleRows = eligibleRows.Concat(channelRows);
        }

        if (productIds is { Count: > 0 })
            eligibleRows = eligibleRows.Where(x => productIds.Contains(x.Product.Id));

        return eligibleRows.Select(x => new PublicCatalogOfferCandidate(
            x.Offer.Id, x.Product.Id, x.Shop.Id, x.Product.SupplierId, x.Product.CategoryId,
            x.Product.Title, x.Product.Slug, x.Product.Description, x.Product.ProductType,
            x.Product.SalesModel, x.Product.FulfillmentMethod, x.Product.CreatedAt,
            null,
            x.Shop.Name, x.Shop.Slug,
            x.Shop.ShopType == ShopType.InPerson ? SalesChannel.InPerson : SalesChannel.Online,
            x.Offer.OriginalPriceMinor, x.Offer.DiscountPercent, x.Offer.FinalPriceMinor,
            x.Offer.ProductVariantId, x.Offer.ProductSessionId, x.Offer.StartDateUtc,
            x.Offer.EndDateUtc, x.Offer.Version, x.Offer.UpdatedAt));
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
        => ReadAsync(BuildEffectiveCandidatesQuery(
            moduleCategoryId,
            categoryId,
            shopId,
            shopSlug,
            productSlug,
            search,
            salesModel,
            atUtc), ct);

    internal IQueryable<PublicCatalogOfferCandidate> BuildEffectiveCandidatesQuery(
        int? moduleCategoryId,
        int? categoryId,
        Guid? shopId,
        string? shopSlug,
        string? productSlug,
        string? search,
        SalesModel? salesModel,
        DateTimeOffset atUtc)
    {
        IReadOnlyCollection<int> categories = categoryId.HasValue
            ? [categoryId.Value]
            : moduleCategoryId.HasValue ? [moduleCategoryId.Value] : [];
        var offers = BuildEffectiveOffers(atUtc, search);
        var products = BuildEffectiveProducts(categories, productSlug, salesModel);
        var shops = BuildEffectiveShops(shopId, shopSlug, includeInPerson: true);
        return BuildCandidateQuery(offers, products, shops);
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

    private IQueryable<Domain.Aggregates.Shop> BuildEffectiveShops(
        Guid? shopId,
        string? shopSlug,
        bool includeInPerson)
    {
        var query = db.Shops.AsNoTracking()
            .Where(x => x.Status == ShopStatus.Active
                && (x.ShopType == ShopType.Online
                    || (includeInPerson && x.ShopType == ShopType.InPerson)));
        if (shopId.HasValue)
            query = query.Where(x => x.Id == shopId.Value);
        if (!string.IsNullOrWhiteSpace(shopSlug))
        {
            var value = shopSlug.Trim().ToLowerInvariant();
            query = query.Where(x => x.Slug == value);
        }
        return query;
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
                null,
                shop.Name, shop.Slug,
                shop.ShopType == ShopType.InPerson ? SalesChannel.InPerson : SalesChannel.Online,
                offer.OriginalPriceMinor, offer.DiscountPercent,
                offer.FinalPriceMinor, offer.ProductVariantId, offer.ProductSessionId,
                offer.StartDateUtc, offer.EndDateUtc, offer.Version, offer.UpdatedAt);

    private static async Task<IReadOnlyList<PublicCatalogOfferCandidate>> ReadAsync(
        IQueryable<PublicCatalogOfferCandidate> query, CancellationToken ct) =>
        await query.ToListAsync(ct);
}
