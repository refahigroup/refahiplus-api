using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class EntertainmentSessionRepository(EntertainmentDbContext db) : IEntertainmentSessionRepository
{
    public async Task<EntertainmentCheckoutSession?> GetFreshAsync(Guid id, CancellationToken ct)
    { db.ChangeTracker.Clear(); return await GetAsync(id, ct); }
    public async Task<IReadOnlyList<EntertainmentCheckoutSession>> GetUncertainAsync(CancellationToken ct) => await db.CheckoutSessions.AsNoTracking()
        .Where(x => x.EntertainmentPurchaseId == null && (x.Status == EntertainmentSessionStatus.Unknown || x.Status == EntertainmentSessionStatus.Reserving || x.Status == EntertainmentSessionStatus.Releasing))
        .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
    public Task<EntertainmentCheckoutSession?> GetAsync(Guid id, CancellationToken ct) => db.CheckoutSessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<EntertainmentCheckoutSession?> FindAsync(Guid userId, string key, CancellationToken ct) => db.CheckoutSessions.SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);
    public async Task AddAsync(EntertainmentCheckoutSession session, CancellationToken ct) => await db.CheckoutSessions.AddAsync(session, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<IReadOnlyList<EntertainmentCheckoutSession>> GetExpiredAsync(CancellationToken ct) => await db.CheckoutSessions
        .Where(x => (x.Status == EntertainmentSessionStatus.Reserved || x.Status == EntertainmentSessionStatus.Releasing)
            && x.EntertainmentPurchaseId == null && x.PayableUntil < DateTimeOffset.UtcNow)
        .OrderBy(x => x.CreatedAt).Take(25).ToListAsync(ct);
    public async Task<IReadOnlyList<EntertainmentCheckoutSession>> GetForRedactionAsync(DateTimeOffset createdBefore, CancellationToken ct) => await db.CheckoutSessions
        .Where(x => x.CreatedAt < createdBefore && x.RequestProtected != ""
            && (x.Status == EntertainmentSessionStatus.Confirmed && db.Orders.Any(p => p.Id == x.EntertainmentPurchaseId &&
                (p.Status == EntertainmentPurchaseStatus.Completed || p.Status == EntertainmentPurchaseStatus.Cancelled || p.Status == EntertainmentPurchaseStatus.Refunded)) || x.Status == EntertainmentSessionStatus.Released || x.Status == EntertainmentSessionStatus.Expired))
        .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
}
