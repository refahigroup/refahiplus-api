using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

internal sealed class AabsarRepository(PoolsDbContext db) : IAabsarRepository
{
    public Task<AabsarCart?> CartAsync(Guid userId, CancellationToken ct)
        => db.AabsarCarts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public Task<AabsarFulfillment?> FulfillmentAsync(Guid purchaseId, CancellationToken ct)
        => db.AabsarFulfillments.SingleOrDefaultAsync(x => x.PurchaseId == purchaseId, ct);
    public async Task<IReadOnlyDictionary<Guid, AabsarShowtime>> ShowtimesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var keys = ids.Distinct().ToArray();
        return await db.AabsarShowtimes.AsNoTracking().Where(x => keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }
    public async Task<IReadOnlyList<AabsarFulfillment>> UnresolvedAsync(int limit, CancellationToken ct)
        => await (from f in db.AabsarFulfillments.AsNoTracking()
                  join p in db.Purchases.AsNoTracking() on f.PurchaseId equals p.Id
                  where f.Status == AabsarOperation.IssueUnknown || f.Status == AabsarOperation.CancelUnknown ||
                        f.Status == AabsarOperation.Sending || f.Status == AabsarOperation.Cancelling
                  orderby p.CreatedAt, f.PurchaseId
                  select f).Take(limit).ToArrayAsync(ct);

    public void Add(AabsarCart cart) => db.AabsarCarts.Add(cart);
    public void Add(AabsarFulfillment fulfillment) => db.AabsarFulfillments.Add(fulfillment);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task RedactContactsAsync(DateTimeOffset cutoff, CancellationToken ct)
        => db.AabsarFulfillments.Where(x => x.ContactCiphertext != null &&
                db.Purchases.Any(p => p.Id == x.PurchaseId && p.CreatedAt < cutoff))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ContactCiphertext, (string?)null), ct);
}
