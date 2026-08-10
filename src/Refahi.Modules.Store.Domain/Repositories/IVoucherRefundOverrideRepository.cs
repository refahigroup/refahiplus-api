using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IVoucherRefundOverrideRepository
{
    Task<VoucherRefundOverride?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<VoucherRefundOverride?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<VoucherRefundOverride?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<VoucherRefundOverrideAttempt>> GetAttemptsAsync(Guid overrideId, CancellationToken ct = default);
    Task AddAsync(VoucherRefundOverride value, CancellationToken ct = default);
    Task AddAttemptAsync(VoucherRefundOverrideAttempt value, CancellationToken ct = default);
}
