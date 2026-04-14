using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IProductSessionRepository
{
    Task<ProductSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ProductSession>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<List<ProductSession>> GetByProductIdAndDateAsync(
        Guid productId, DateOnly date, CancellationToken ct = default);
    Task<List<ProductSession>> GetAvailableByProductIdAsync(
        Guid productId, CancellationToken ct = default);
    Task UpdateAsync(ProductSession session, CancellationToken ct = default);
}
