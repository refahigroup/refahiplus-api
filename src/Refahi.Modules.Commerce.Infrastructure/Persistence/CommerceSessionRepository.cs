using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Commerce.Domain;
namespace Refahi.Modules.Commerce.Infrastructure.Persistence;

public sealed class CommerceSessionRepository(CommerceDbContext db) : ICommerceSessionRepository
{
    public Task<CommerceCheckoutSession?> GetAsync(Guid id, CancellationToken ct) => db.CheckoutSessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<CommerceCheckoutSession?> FindAsync(Guid userId, string key, CancellationToken ct) => db.CheckoutSessions.SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);
    public async Task AddAsync(CommerceCheckoutSession session, CancellationToken ct) => await db.CheckoutSessions.AddAsync(session, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<IReadOnlyList<CommerceCheckoutSession>> GetExpiredAsync(CancellationToken ct) => await db.CheckoutSessions
        .Where(x => (x.Status == CommerceSessionStatus.Reserved || x.Status == CommerceSessionStatus.Releasing)
            && x.CommerceOrderId == null && x.PayableUntil < DateTimeOffset.UtcNow)
        .OrderBy(x => x.CreatedAt).Take(25).ToListAsync(ct);
    public async Task<IReadOnlyList<CommerceCheckoutSession>> GetForRedactionAsync(DateTimeOffset createdBefore, CancellationToken ct) => await db.CheckoutSessions
        .Where(x => x.CreatedAt < createdBefore && x.RequestProtected != ""
            && (x.Status == CommerceSessionStatus.Confirmed || x.Status == CommerceSessionStatus.Released || x.Status == CommerceSessionStatus.Expired))
        .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
}
