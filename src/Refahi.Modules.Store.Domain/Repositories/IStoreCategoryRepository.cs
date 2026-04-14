using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IStoreCategoryRepository
{
    Task<StoreCategory?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<StoreCategory>> GetAllAsync(CancellationToken ct = default);
    Task<List<StoreCategory>> GetAllActiveAsync(CancellationToken ct = default);
    Task<List<StoreCategory>> GetByParentIdAsync(int? parentId, CancellationToken ct = default);
    Task AddAsync(StoreCategory category, CancellationToken ct = default);
    Task UpdateAsync(StoreCategory category, CancellationToken ct = default);
}
