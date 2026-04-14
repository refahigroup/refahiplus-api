using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ShopRepository : IShopRepository
{
    private readonly StoreDbContext _db;

    public ShopRepository(StoreDbContext db) => _db = db;

    public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Shops.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Shops.FirstOrDefaultAsync(s => s.Slug == slug, ct);

    public Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default)
        => _db.Shops.FirstOrDefaultAsync(s => s.ProviderId == providerId, ct);

    public async Task<(List<Shop> Items, int Total)> GetPagedAsync(
        ShopType? shopType, ShopStatus? status, int page, int size, CancellationToken ct = default)
    {
        var query = _db.Shops.AsQueryable();

        if (shopType.HasValue)
            query = query.Where(s => s.ShopType == shopType.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Shops.AnyAsync(s => s.Slug == slug, ct);

    public Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default)
        => _db.Shops.AnyAsync(s => s.ProviderId == providerId, ct);

    public async Task AddAsync(Shop shop, CancellationToken ct = default)
    {
        await _db.Shops.AddAsync(shop, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Shop shop, CancellationToken ct = default)
    {
        _db.Shops.Update(shop);
        await _db.SaveChangesAsync(ct);
    }
}
