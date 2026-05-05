using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class BannerRepository : IBannerRepository
{
    private readonly StoreDbContext _db;

    public BannerRepository(StoreDbContext db) => _db = db;

    public Task<Banner?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Banner>> GetActiveAsync(CancellationToken ct = default)
        => _db.Banners
            .Where(b => b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

    public Task<List<Banner>> GetActiveByModuleAsync(int moduleId, CancellationToken ct = default)
        => _db.Banners
            .Where(b => b.IsActive && !b.IsDeleted && b.ModuleId == moduleId)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

    public Task<List<Banner>> GetActiveByShopAsync(Guid shopId, CancellationToken ct = default)
        => _db.Banners
            .Where(b => b.IsActive && !b.IsDeleted && b.ShopId == shopId)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

    public Task<List<Banner>> GetAllAsync(int? moduleId = null, Guid? shopId = null, CancellationToken ct = default)
    {
        var query = _db.Banners.Where(b => !b.IsDeleted);
        if (moduleId.HasValue)
            query = query.Where(b => b.ModuleId == moduleId.Value);
        if (shopId.HasValue)
            query = query.Where(b => b.ShopId == shopId.Value);
        return query.OrderBy(b => b.SortOrder).ToListAsync(ct);
    }

    public async Task AddAsync(Banner banner, CancellationToken ct = default)
    {
        await _db.Banners.AddAsync(banner, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Banner banner, CancellationToken ct = default)
    {
        _db.Banners.Update(banner);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Banner banner, CancellationToken ct = default)
    {
        banner.Delete();
        _db.Banners.Update(banner);
        await _db.SaveChangesAsync(ct);
    }
}
