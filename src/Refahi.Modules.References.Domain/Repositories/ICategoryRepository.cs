using Refahi.Modules.References.Domain.Entities;

namespace Refahi.Modules.References.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Category>> GetAllAsync(CancellationToken ct = default);
    Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default);
    Task<List<Category>> GetByParentIdAsync(int? parentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the root category ID plus all active descendant IDs using a recursive CTE.
    /// Returns an empty list if the root does not exist or is inactive.
    /// </summary>
    Task<IReadOnlyList<int>> GetSubtreeIdsAsync(int rootId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Category category, CancellationToken ct = default);
    Task UpdateAsync(Category category, CancellationToken ct = default);
}
