using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class StoreCategoryRepository : IStoreCategoryRepository
{
    private readonly StoreDbContext _db;

    public StoreCategoryRepository(StoreDbContext db) => _db = db;

    public Task<StoreCategory?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<StoreCategory>> GetAllAsync(CancellationToken ct = default)
        => _db.Categories.OrderBy(c => c.SortOrder).ToListAsync(ct);

    public Task<List<StoreCategory>> GetAllActiveAsync(CancellationToken ct = default)
        => _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public Task<List<StoreCategory>> GetByParentIdAsync(int? parentId, CancellationToken ct = default)
        => _db.Categories
            .Where(c => c.ParentId == parentId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public async Task AddAsync(StoreCategory category, CancellationToken ct = default)
    {
        await _db.Categories.AddAsync(category, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StoreCategory category, CancellationToken ct = default)
    {
        _db.Categories.Update(category);
        await _db.SaveChangesAsync(ct);
    }
}
