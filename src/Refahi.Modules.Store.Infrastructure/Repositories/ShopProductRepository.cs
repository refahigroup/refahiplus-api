using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ShopProductRepository : IShopProductRepository
{
    private readonly StoreDbContext _db;

    public ShopProductRepository(StoreDbContext db) => _db = db;

    public Task<ShopProduct?> GetAsync(Guid shopId, Guid productId, CancellationToken ct = default)
        => _db.ShopProducts
            .FirstOrDefaultAsync(sp => sp.ShopId == shopId && sp.ProductId == productId && !sp.IsDeleted, ct);

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

    public async Task UpdateAsync(ShopProduct shopProduct, CancellationToken ct = default)
    {
        _db.ShopProducts.Update(shopProduct);
        await _db.SaveChangesAsync(ct);
    }
}
