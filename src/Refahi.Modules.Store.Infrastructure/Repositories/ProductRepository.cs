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

    public Task<List<Product>> GetByShopIdAsync(Guid shopId, CancellationToken ct = default)
        => _db.Products
            .Where(p => p.ShopId == shopId && !p.IsDeleted)
            .ToListAsync(ct);

    public async Task<(List<Product> Items, int Total)> GetPagedAsync(
        int? categoryId, Guid? shopId, long? minPrice, long? maxPrice,
        short? salesModel, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Products.Where(p => !p.IsDeleted && p.IsAvailable);

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);
        if (shopId.HasValue)
            q = q.Where(p => p.ShopId == shopId.Value);
        if (minPrice.HasValue)
            q = q.Where(p => p.PriceMinor >= minPrice.Value);
        if (maxPrice.HasValue)
            q = q.Where(p => p.PriceMinor <= maxPrice.Value);
        if (salesModel.HasValue)
            q = q.Where(p => (short)p.SalesModel == salesModel.Value);

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

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Slug == slug, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _db.Products.AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync(ct);
    }
}
