using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly StoreDbContext _db;

    public ProductRepository(StoreDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Specifications)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

    public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Specifications)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted, ct);

    public async Task<(List<Product> Items, int Total)> GetPagedAsync(
        Guid? shopId, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Product> q;

        if (shopId.HasValue)
        {
            q = _db.ShopProducts
                .Where(sp => sp.ShopId == shopId.Value && sp.IsActive && !sp.IsDeleted)
                .Join(_db.Products,
                    sp => sp.ProductId,
                    p => p.Id,
                    (_, p) => p)
                .Where(p => !p.IsDeleted && p.IsAvailable);
        }
        else
        {
            q = _db.Products.Where(p => !p.IsDeleted && p.IsAvailable);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<Product> Items, int Total)> SearchAsync(
        string query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Products
            .Where(p => !p.IsDeleted && p.IsAvailable &&
                        (p.Title.Contains(query) || (p.Description != null && p.Description.Contains(query))));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<Product> Items, int Total)> SearchAsync(
        string query, IReadOnlyList<Guid> allowedAgreementProductIds,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Products.Where(p =>
            !p.IsDeleted && p.IsAvailable
            && allowedAgreementProductIds.Contains(p.AgreementProductId)
            && (p.Title.Contains(query) || (p.Description != null && p.Description.Contains(query))));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await _db.Products
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Images)
            .ToListAsync(ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Slug == slug, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _db.Products.AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default)
        => _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Specifications)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(List<Product> Items, int Total)> GetPagedAdminAsync(
        Guid? shopId, bool? isDeleted,
        int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Product> q;

        if (shopId.HasValue)
        {
            q = _db.ShopProducts
                .Where(sp => sp.ShopId == shopId.Value && !sp.IsDeleted)
                .Join(_db.Products,
                    sp => sp.ProductId,
                    p => p.Id,
                    (_, p) => p);
        }
        else
        {
            q = _db.Products.AsQueryable();
        }

        if (isDeleted.HasValue)
            q = q.Where(p => p.IsDeleted == isDeleted.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync(ct);
    }
}
