using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ShopProductRepository : IShopProductRepository
{
    private readonly StoreDbContext _db;

    public ShopProductRepository(StoreDbContext db) => _db = db;

    public async Task<(IReadOnlyList<ProductOfferingReadModel> Items, int Total)> GetDisplayableProductsAsync(
        IReadOnlyList<Guid> stockBasedAgreementProductIds,
        IReadOnlyList<Guid> sessionBasedAgreementProductIds,
        DateOnly today,
        string? searchQuery,
        string sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {

        try
        {



            var allowedIds = stockBasedAgreementProductIds
                .Concat(sessionBasedAgreementProductIds)
                .Distinct()
                .ToList();

            if (allowedIds.Count == 0)
                return ([], 0);

            var normalizedSearch = searchQuery?.Trim();
            var query =
                from offering in _db.ShopProductVariants.AsNoTracking()
                join shopProduct in _db.ShopProducts.AsNoTracking() on offering.ShopProductId equals shopProduct.Id
                join product in _db.Products.AsNoTracking() on shopProduct.ProductId equals product.Id
                join variant in _db.ProductVariants.AsNoTracking() on offering.ProductVariantId equals variant.Id
                join shop in _db.Shops.AsNoTracking() on shopProduct.ShopId equals shop.Id
                where allowedIds.Contains(product.AgreementProductId)
                      && shop.Status == ShopStatus.Active
                      && shopProduct.IsActive && !shopProduct.IsDeleted
                      && offering.IsActive && !offering.IsDeleted
                      && product.IsAvailable && !product.IsDeleted
                      && variant.IsAvailable
                      && offering.PriceMinor > 0
                      && (offering.DiscountedPriceMinor == null
                          || (offering.DiscountedPriceMinor > 0
                              && offering.DiscountedPriceMinor < offering.PriceMinor))
                      && ((stockBasedAgreementProductIds.Contains(product.AgreementProductId) && variant.StockCount > 0)
                      || (sessionBasedAgreementProductIds.Contains(product.AgreementProductId)
                          && ((variant.CapacityType == VariantCapacityType.Unlimited || variant.Capacity > 0)
                              || _db.ProductSessions.Any(session =>
                                  session.ProductId == product.Id
                                  && session.Date >= today
                                  && session.IsActive
                                  && !session.IsCancelled
                                  && session.SoldCount < session.Capacity))))
                select new
                {
                    Offering = offering,
                    ShopProduct = shopProduct,
                    Product = product,
                    Variant = variant,
                    Shop = shop,
                    EffectivePrice = offering.DiscountedPriceMinor ?? offering.PriceMinor
                };

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(x =>
                    x.Product.Title.Contains(normalizedSearch)
                    || x.Shop.Name.Contains(normalizedSearch)
                    || _db.ProductVariantCombinations.Any(c =>
                        c.ProductVariantId == x.Variant.Id
                        && _db.VariantAttributeValues.Any(v =>
                            v.Id == c.VariantAttributeValueId && v.Value.Contains(normalizedSearch))));
            }

            // Page distinct products in SQL, then choose the representative offering from the
            // bounded page in memory. This keeps pagination product-based without loading the
            // full offering set and avoids provider-specific GroupBy/First translation.
            var productGroups = query
                .GroupBy(x => new { ProductId = x.Product.Id, x.Product.CreatedAt })
                .Select(group => new
                {
                    group.Key.ProductId,
                    ProductCreatedAt = group.Key.CreatedAt,
                    EffectivePrice = group.Min(x => x.EffectivePrice)
                });

            var total = await productGroups.CountAsync(ct);
            var orderedProducts = sort switch
            {
                "price-asc" => productGroups
                    .OrderBy(x => x.EffectivePrice)
                    .ThenBy(x => x.ProductId),
                "price-desc" => productGroups
                    .OrderByDescending(x => x.EffectivePrice)
                    .ThenBy(x => x.ProductId),
                _ => productGroups
                    .OrderByDescending(x => x.ProductCreatedAt)
                    .ThenBy(x => x.ProductId)
            };

            var productPage = await orderedProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            var productIds = productPage.Select(x => x.ProductId).ToList();
            if (productIds.Count == 0)
                return ([], total);

            var candidateRows = await query
                .Where(x => productIds.Contains(x.Product.Id))
                .Select(x => new
                {
                    ProductId = x.Product.Id,
                    x.Product.AgreementProductId,
                    ProductVariantId = x.Variant.Id,
                    ShopProductVariantId = x.Offering.Id,
                    ShopId = x.Shop.Id,
                    ProductTitle = x.Product.Title,
                    ProductSlug = x.Product.Slug,
                    ShopName = x.Shop.Name,
                    ShopSlug = x.Shop.Slug,
                    VariantImageUrl = x.Variant.ImageUrl,
                    x.Offering.PriceMinor,
                    x.Offering.DiscountedPriceMinor,
                    EffectivePrice = x.Offering.DiscountedPriceMinor ?? x.Offering.PriceMinor,
                    OfferingCreatedAt = x.Offering.CreatedAt,
                    x.Product.CreatedAt
                })
                .ToListAsync(ct);

            var productOrder = productPage
                .Select((item, index) => new { item.ProductId, index })
                .ToDictionary(x => x.ProductId, x => x.index);
            var rows = candidateRows
                .GroupBy(x => x.ProductId)
                .Select(group => group
                    .OrderBy(x => x.EffectivePrice)
                    .ThenByDescending(x => x.OfferingCreatedAt)
                    .ThenBy(x => x.ShopProductVariantId)
                    .First())
                .OrderBy(x => productOrder[x.ProductId])
                .ToList();

            var variantIds = rows.Select(x => x.ProductVariantId).ToList();

            var labels = await (
                from combination in _db.ProductVariantCombinations.AsNoTracking()
                join attribute in _db.VariantAttributes.AsNoTracking() on combination.VariantAttributeId equals attribute.Id
                join value in _db.VariantAttributeValues.AsNoTracking() on combination.VariantAttributeValueId equals value.Id
                where variantIds.Contains(combination.ProductVariantId)
                orderby attribute.SortOrder, value.SortOrder
                select new { combination.ProductVariantId, attribute.Name, value.Value })
                .ToListAsync(ct);

            var labelMap = labels
                .GroupBy(x => x.ProductVariantId)
                .ToDictionary(g => g.Key, g => string.Join("، ", g.Select(x => $"{x.Name}: {x.Value}")));

            var imageMap = await _db.ProductImages.AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId))
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.SortOrder)
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, ImageUrl = g.Select(x => x.ImageUrl).FirstOrDefault() })
                .ToDictionaryAsync(x => x.ProductId, x => x.ImageUrl, ct);

            var items = rows.Select(x => new ProductOfferingReadModel(
                x.ProductId,
                x.AgreementProductId,
                x.ProductVariantId,
                x.ShopProductVariantId,
                x.ShopId,
                x.ProductTitle,
                x.ProductSlug,
                x.ShopName,
                x.ShopSlug,
                labelMap.GetValueOrDefault(x.ProductVariantId) ?? string.Empty,
                x.VariantImageUrl ?? imageMap.GetValueOrDefault(x.ProductId),
                x.PriceMinor,
                x.DiscountedPriceMinor,
                x.CreatedAt)).ToList();

            return (items, total);
        }
        catch(Exception exp)
        {
            throw exp;
        }
    }

    public Task<ShopProduct?> GetAsync(Guid shopId, Guid productId, CancellationToken ct = default)
        => _db.ShopProducts
            .FirstOrDefaultAsync(sp => sp.ShopId == shopId && sp.ProductId == productId && !sp.IsDeleted, ct);

    public Task<ShopProduct?> GetWithVariantOfferingsAsync(Guid shopId, Guid productId, CancellationToken ct = default)
        => _db.ShopProducts
            .Include(sp => sp.VariantOfferings)
            .FirstOrDefaultAsync(sp => sp.ShopId == shopId && sp.ProductId == productId && !sp.IsDeleted, ct);

    public async Task<ShopProduct?> GetBestDisplayableForProductAsync(
        Guid productId,
        SalesModel salesModel,
        DateOnly today,
        CancellationToken ct = default)
    {
        var candidates =
            from offering in _db.ShopProductVariants.AsNoTracking()
            join shopProduct in _db.ShopProducts.AsNoTracking() on offering.ShopProductId equals shopProduct.Id
            join variant in _db.ProductVariants.AsNoTracking() on offering.ProductVariantId equals variant.Id
            join shop in _db.Shops.AsNoTracking() on shopProduct.ShopId equals shop.Id
            where shopProduct.ProductId == productId
                  && shop.Status == ShopStatus.Active
                  && shopProduct.IsActive && !shopProduct.IsDeleted
                  && offering.IsActive && !offering.IsDeleted
                  && variant.IsAvailable
                  && offering.PriceMinor > 0
                  && (offering.DiscountedPriceMinor == null
                      || (offering.DiscountedPriceMinor > 0
                          && offering.DiscountedPriceMinor < offering.PriceMinor))
                  && (salesModel == SalesModel.StockBased
                      ? variant.StockCount > 0
                      : ((variant.CapacityType == VariantCapacityType.Unlimited || variant.Capacity > 0)
                         || _db.ProductSessions.Any(session =>
                             session.ProductId == productId
                             && session.Date >= today
                             && session.IsActive
                             && !session.IsCancelled
                             && session.SoldCount < session.Capacity)))
            select new
            {
                shopProduct.Id,
                EffectivePrice = offering.DiscountedPriceMinor ?? offering.PriceMinor,
                offering.CreatedAt,
                OfferingId = offering.Id
            };

        var shopProductId = await candidates
            .OrderBy(x => x.EffectivePrice)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.OfferingId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (!shopProductId.HasValue)
            return null;

        return await _db.ShopProducts
            .Include(sp => sp.VariantOfferings)
            .FirstOrDefaultAsync(sp => sp.Id == shopProductId.Value, ct);
    }

    public async Task<(List<ShopProduct> Items, int Total)> GetByShopAsync(
        Guid shopId, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ShopProducts.Where(sp => sp.ShopId == shopId && !sp.IsDeleted);

        if (isActive.HasValue)
            q = q.Where(sp => sp.IsActive == isActive.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(sp => sp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<ShopProduct> Items, int Total)> GetByProductAsync(
        Guid productId, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ShopProducts.Where(sp => sp.ProductId == productId && !sp.IsDeleted);

        if (isActive.HasValue)
            q = q.Where(sp => sp.IsActive == isActive.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(sp => sp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<ShopProduct>> ListForVariantBackfillAsync(
        Guid? shopId = null,
        Guid? productId = null,
        CancellationToken ct = default)
    {
        var q = _db.ShopProducts
            .Include(sp => sp.VariantOfferings)
            .Where(sp => !sp.IsDeleted);

        if (shopId.HasValue)
            q = q.Where(sp => sp.ShopId == shopId.Value);

        if (productId.HasValue)
            q = q.Where(sp => sp.ProductId == productId.Value);

        return await q
            .OrderBy(sp => sp.ShopId)
            .ThenBy(sp => sp.ProductId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveShopIdsByAgreementProductIdsAsync(
        IEnumerable<Guid> agreementProductIds, CancellationToken ct = default)
    {
        var idList = agreementProductIds as ICollection<Guid> ?? agreementProductIds.ToList();
        if (idList.Count == 0)
            return [];

        return await _db.ShopProducts
            .Join(
                _db.Products.Where(p => idList.Contains(p.AgreementProductId) && p.IsAvailable && !p.IsDeleted),
                sp => sp.ProductId,
                p => p.Id,
                (sp, _) => sp)
            .Where(sp => sp.IsActive && !sp.IsDeleted)
            .Select(sp => sp.ShopId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDisplayableShopIdsByAgreementProductIdsAsync(
        IReadOnlyList<Guid> apIds, CancellationToken ct = default)
    {
        if (apIds.Count == 0)
            return [];

        return await (
            from sp in _db.ShopProducts.Where(sp => sp.IsActive && !sp.IsDeleted)
            join p in _db.Products.Where(p => p.IsAvailable && !p.IsDeleted && apIds.Contains(p.AgreementProductId))
                on sp.ProductId equals p.Id
            select sp.ShopId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Guid> ProductIds, int Total)> GetDisplayableProductIdsByAgreementProductIdsAsync(
        IReadOnlyList<Guid> apIds, Guid? shopId, int page, int pageSize, CancellationToken ct = default)
    {
        if (apIds.Count == 0)
            return ([], 0);

        var q = (
            from sp in _db.ShopProducts.Where(sp => sp.IsActive && !sp.IsDeleted)
            join p in _db.Products.Where(p => p.IsAvailable && !p.IsDeleted && apIds.Contains(p.AgreementProductId))
                on sp.ProductId equals p.Id
            where !shopId.HasValue || sp.ShopId == shopId.Value
            orderby p.CreatedAt descending
            select p.Id)
            .Distinct();

        var total = await q.CountAsync(ct);
        var productIds = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (productIds, total);
    }

    public async Task<IReadOnlyDictionary<Guid, ShopProduct>> GetForProductsAsync(
        IReadOnlyList<Guid> productIds, Guid? shopId = null, CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ShopProduct>();

        var q = _db.ShopProducts
            .Where(sp => productIds.Contains(sp.ProductId) && sp.IsActive && !sp.IsDeleted);

        if (shopId.HasValue)
            q = q.Where(sp => sp.ShopId == shopId.Value);

        var items = await q.ToListAsync(ct);

        // When multiple shops carry the same product, pick the one with the lowest discounted price.
        return items
            .GroupBy(sp => sp.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(sp => sp.DiscountedPrice).First());
    }

    public async Task AddAsync(ShopProduct shopProduct, CancellationToken ct = default)
    {
        await _db.ShopProducts.AddAsync(shopProduct, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddVariantOfferingsAsync(
        ShopProduct shopProduct,
        IReadOnlyList<ShopProductVariant> offerings,
        CancellationToken ct = default)
    {
        if (offerings.Count == 0)
            return;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        foreach (var offering in offerings)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO store.shop_product_variants
                    (""Id"", ""ShopProductId"", ""ProductVariantId"", ""PriceMinor"", ""DiscountedPriceMinor"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                    ({offering.Id}, {offering.ShopProductId}, {offering.ProductVariantId}, {offering.PriceMinor}, {offering.DiscountedPriceMinor}, {offering.IsActive}, {offering.IsDeleted}, {offering.CreatedAt}, {offering.UpdatedAt})
                ON CONFLICT (""ShopProductId"", ""ProductVariantId"") WHERE ""IsDeleted"" = false
                DO NOTHING", ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task UpsertVariantOfferingAsync(
        ShopProduct shopProduct,
        ShopProductVariant offering,
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO store.shop_product_variants
                (""Id"", ""ShopProductId"", ""ProductVariantId"", ""PriceMinor"", ""DiscountedPriceMinor"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                ({offering.Id}, {offering.ShopProductId}, {offering.ProductVariantId}, {offering.PriceMinor}, {offering.DiscountedPriceMinor}, {offering.IsActive}, false, {offering.CreatedAt}, {offering.UpdatedAt})
            ON CONFLICT (""ShopProductId"", ""ProductVariantId"") WHERE ""IsDeleted"" = false
            DO UPDATE SET
                ""PriceMinor"" = EXCLUDED.""PriceMinor"",
                ""DiscountedPriceMinor"" = EXCLUDED.""DiscountedPriceMinor"",
                ""IsActive"" = EXCLUDED.""IsActive"",
                ""UpdatedAt"" = EXCLUDED.""UpdatedAt""", ct);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE store.shop_products
            SET ""UpdatedAt"" = {shopProduct.UpdatedAt}
            WHERE ""Id"" = {shopProduct.Id} AND ""IsDeleted"" = false", ct);

        await transaction.CommitAsync(ct);
    }

    public async Task UpdateAsync(ShopProduct shopProduct, CancellationToken ct = default)
    {
        if (_db.Entry(shopProduct).State == EntityState.Detached)
            _db.ShopProducts.Update(shopProduct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new StoreConcurrencyException(ex);
        }
    }
}
