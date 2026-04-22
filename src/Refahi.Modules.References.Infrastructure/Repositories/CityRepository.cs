using Microsoft.EntityFrameworkCore;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Modules.References.Infrastructure.Persistence.Context;

namespace Refahi.Modules.References.Infrastructure.Repositories;

public class CityRepository : ICityRepository
{
    private readonly ReferencesDbContext _db;

    public CityRepository(ReferencesDbContext db) => _db = db;

    public Task<City?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Cities.Include(c => c.Province).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<City?> GetBySlugAsync(int provinceId, string slug, CancellationToken ct = default)
        => _db.Cities.Include(c => c.Province).FirstOrDefaultAsync(c => c.ProvinceId == provinceId && c.Slug == slug, ct);

    public async Task<List<City>> GetAllAsync(int? provinceId = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.Cities.Include(c => c.Province).AsQueryable();

        if (provinceId.HasValue)
            query = query.Where(c => c.ProvinceId == provinceId.Value);

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, int provinceId, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Cities.Where(c => c.Slug == slug && c.ProvinceId == provinceId);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(City city, CancellationToken ct = default)
    {
        await _db.Cities.AddAsync(city, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(City city, CancellationToken ct = default)
    {
        _db.Cities.Update(city);
        await _db.SaveChangesAsync(ct);
    }
}
