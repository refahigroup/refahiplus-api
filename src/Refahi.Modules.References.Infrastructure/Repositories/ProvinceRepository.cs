using Microsoft.EntityFrameworkCore;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Modules.References.Infrastructure.Persistence.Context;

namespace Refahi.Modules.References.Infrastructure.Repositories;

public class ProvinceRepository : IProvinceRepository
{
    private readonly ReferencesDbContext _db;

    public ProvinceRepository(ReferencesDbContext db) => _db = db;

    public Task<Province?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Provinces.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Province?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Provinces.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<List<Province>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.Provinces.AsQueryable();

        if (activeOnly)
            query = query.Where(p => p.IsActive);

        return await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Provinces.Where(p => p.Slug == slug);

        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Province province, CancellationToken ct = default)
    {
        await _db.Provinces.AddAsync(province, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Province province, CancellationToken ct = default)
    {
        _db.Provinces.Update(province);
        await _db.SaveChangesAsync(ct);
    }
}
