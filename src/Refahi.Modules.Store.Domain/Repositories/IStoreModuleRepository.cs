using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IStoreModuleRepository
{
    Task<StoreModule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<StoreModule?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<StoreModule>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(StoreModule module, CancellationToken ct = default);
    Task UpdateAsync(StoreModule module, CancellationToken ct = default);
}
