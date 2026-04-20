using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default);
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task UpdateAsync(Cart cart, CancellationToken ct = default);
    Task DeleteAsync(Cart cart, CancellationToken ct = default);
}
