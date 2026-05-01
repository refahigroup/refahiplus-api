using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IBannerRepository
{
    Task<Banner?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Banner>> GetActiveAsync(CancellationToken ct = default);
    Task<List<Banner>> GetAllAsync(int? moduleId = null, CancellationToken ct = default);
    Task AddAsync(Banner banner, CancellationToken ct = default);
    Task UpdateAsync(Banner banner, CancellationToken ct = default);
    Task DeleteAsync(Banner banner, CancellationToken ct = default);
}
