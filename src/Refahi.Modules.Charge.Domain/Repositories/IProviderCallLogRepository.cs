using Refahi.Modules.Charge.Domain.Aggregates;

namespace Refahi.Modules.Charge.Domain.Repositories;

public interface IProviderCallLogRepository
{
    Task AddAsync(ProviderCallLog log, CancellationToken ct = default);
    Task<IReadOnlyList<ProviderCallLog>> GetForChargeRequestAsync(Guid requestId, int skip, int take, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int take, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
