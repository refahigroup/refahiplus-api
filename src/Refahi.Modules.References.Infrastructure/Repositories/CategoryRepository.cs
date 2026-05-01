using Microsoft.EntityFrameworkCore;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Modules.References.Infrastructure.Persistence.Context;

namespace Refahi.Modules.References.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ReferencesDbContext _db;

    public CategoryRepository(ReferencesDbContext db) => _db = db;

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<Category>> GetAllAsync(CancellationToken ct = default)
        => _db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default)
        => _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<List<Category>> GetByParentIdAsync(int? parentId, CancellationToken ct = default)
        => _db.Categories
            .Where(c => c.ParentId == parentId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetSubtreeIdsAsync(int rootId, CancellationToken ct = default)
    {
        // Recursive CTE: returns rootId + all active descendant IDs.
        // Column casing matches the migration (PascalCase, no snake_case convention applied).
        const string sql = """
            WITH RECURSIVE subtree AS (
                SELECT "Id" FROM "references"."categories"
                WHERE "Id" = {0} AND "IsActive" = true
                UNION ALL
                SELECT c."Id" FROM "references"."categories" c
                INNER JOIN subtree s ON c."ParentId" = s."Id"
                WHERE c."IsActive" = true
            )
            SELECT "Id" AS "Value" FROM subtree
            """;

        return await _db.Database
            .SqlQueryRaw<int>(sql, rootId)
            .ToListAsync(ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Categories.Where(c => c.Slug == slug);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Category category, CancellationToken ct = default)
    {
        await _db.Categories.AddAsync(category, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Category category, CancellationToken ct = default)
    {
        _db.Categories.Update(category);
        await _db.SaveChangesAsync(ct);
    }
}
