using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;

public sealed class ProviderCallLogRepository : IProviderCallLogRepository
{
    private readonly ChargeDbContext _db;
    public ProviderCallLogRepository(ChargeDbContext db) => _db = db;

    public Task AddAsync(ProviderCallLog log, CancellationToken ct = default) =>
        _db.ProviderCallLogs.AddAsync(log, ct).AsTask();

    public async Task<IReadOnlyList<ProviderCallLog>> GetForChargeRequestAsync(Guid requestId, int skip, int take, CancellationToken ct = default) =>
        await _db.ProviderCallLogs.AsNoTracking()
            .Where(x => x.ChargeRequestId == requestId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip).Take(take).ToListAsync(ct);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int take, CancellationToken ct = default)
    {
        var ids = await _db.ProviderCallLogs.AsNoTracking()
            .Where(x => x.CreatedAt < cutoffUtc)
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(take).ToListAsync(ct);
        return ids.Count == 0
            ? 0
            : await _db.ProviderCallLogs.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
