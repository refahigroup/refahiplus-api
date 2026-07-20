using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;

public sealed class ChargeRequestRepository : IChargeRequestRepository
{
    private readonly ChargeDbContext _db;

    public ChargeRequestRepository(ChargeDbContext db)
    {
        _db = db;
    }

    public Task<ChargeRequest?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.ChargeRequests
                  .AsSplitQuery()
                  .Include(x => x.Pins)
                  .Include(x => x.Attempts)
                  .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<ChargeRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return _db.ChargeRequests
                  .AsSplitQuery()
                  .Include(x => x.Pins)
                  .Include(x => x.Attempts)
                  .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    } 

    public Task<ChargeRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return _db.ChargeRequests
                  .AsSplitQuery()
                  .Include(x => x.Pins)
                  .Include(x => x.Attempts)
                  .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);

    }
    public Task<ChargeRequest?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default)
    {
        return _db.ChargeRequests
                  .Include(x => x.Pins)
                  .FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);
    }

    public async Task<IReadOnlyList<ChargeRequest>> GetWorkItemsAsync(DateTime nowUtc, int take, CancellationToken ct = default)
    {
        return await _db.ChargeRequests
                        .Where(x => x.Status == ChargeRequestStatus.Paid ||
                                (x.Status == ChargeRequestStatus.ReconciliationPending && x.NextReconciliationAt <= nowUtc) ||
                                (x.Status == ChargeRequestStatus.Processing && x.ProcessingLeaseUntil <= nowUtc) ||
                                (x.Status == ChargeRequestStatus.Refunding &&
                                 (x.NextReconciliationAt == null || x.NextReconciliationAt <= nowUtc) &&
                                 (x.ProcessingLeaseUntil == null || x.ProcessingLeaseUntil <= nowUtc)))
                        .OrderBy(x => x.UpdatedAt)
                        .Take(take)
                        .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChargeRequest>> GetExpiredCandidatesAsync(DateTime nowUtc, int take, CancellationToken ct = default)
    {
        return await _db.ChargeRequests
            .Where(x => x.ExpireAt <= nowUtc &&
                (x.Status == ChargeRequestStatus.Created || x.Status == ChargeRequestStatus.ConvertedToOrder))
            .OrderBy(x => x.ExpireAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ChargeRequest request, CancellationToken ct = default)
    {
        await _db.ChargeRequests.AddAsync(request, ct);
    }

    public Task AddFulfillmentAttemptAsync(ChargeFulfillmentAttempt attempt, CancellationToken ct = default)
    {
        return _db.FulfillmentAttempts.AddAsync(attempt, ct).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }
}
