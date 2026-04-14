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
            .Where(b => b.IsActive)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

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
        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync(ct);
    }
}
