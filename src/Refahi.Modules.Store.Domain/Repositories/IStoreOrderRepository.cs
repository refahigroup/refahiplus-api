using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IStoreOrderRepository
{
    Task<StoreOrder?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken ct = default);
    Task<StoreOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<StoreOrder?> GetByIdAsync(Guid storeOrderId, CancellationToken ct = default) =>
        Task.FromResult<StoreOrder?>(null);
    Task<IReadOnlyList<StoreOrder>> GetByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StoreOrder>>([]);
    Task AddAsync(StoreOrder order, CancellationToken ct = default);
    Task UpdateAsync(StoreOrder order, CancellationToken ct = default);
    Task CommitPaidAsync(Guid orderId, CancellationToken ct = default);
}
