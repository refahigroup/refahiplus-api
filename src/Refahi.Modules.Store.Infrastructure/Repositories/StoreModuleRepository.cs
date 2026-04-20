using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class StoreModuleRepository : IStoreModuleRepository
{
    private readonly StoreDbContext _db;

    public StoreModuleRepository(StoreDbContext db) => _db = db;

    public Task<StoreModule?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Modules.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<StoreModule?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Modules.FirstOrDefaultAsync(m => m.Slug == slug.ToLower(), ct);

    public Task<List<StoreModule>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
        => includeInactive
            ? _db.Modules.OrderBy(m => m.SortOrder).ToListAsync(ct)
            : _db.Modules.Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToListAsync(ct);

    public Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default)
        => _db.Modules.AnyAsync(m =>
            m.Slug == slug.ToLower() &&
            (!excludeId.HasValue || m.Id != excludeId.Value), ct);

    public async Task AddAsync(StoreModule module, CancellationToken ct = default)
    {
        await _db.Modules.AddAsync(module, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StoreModule module, CancellationToken ct = default)
    {
        _db.Modules.Update(module);
        await _db.SaveChangesAsync(ct);
    }
}
