using Refahi.Modules.Charge.Domain.Aggregates;

namespace Refahi.Modules.Charge.Domain.Repositories;

public interface IChargeRequestRepository
{
    Task<ChargeRequest?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ChargeRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ChargeRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<ChargeRequest?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeRequest>> GetWorkItemsAsync(DateTime nowUtc, int take, CancellationToken ct = default);
    Task AddAsync(ChargeRequest request, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
