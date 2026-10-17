using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolPurchaseRepository(PoolsDbContext db, TimeProvider clock) : IPoolPurchaseRepository
{
    public async Task<PoolPurchase?> GetAsync(Guid id, CancellationToken ct)
    {
        // Payment/issuance/cancellation may have committed in another scope while waiting for the lock.
        var tracked = db.ChangeTracker.Entries<PoolPurchase>().SingleOrDefault(x => x.Entity.Id == id);
        if (tracked != null)
        {
            await tracked.ReloadAsync(ct);
            await tracked.Collection(x => x.Requests).LoadAsync(ct);
            foreach (var r in tracked.Entity.Requests) await db.Entry(r).ReloadAsync(ct);
            return tracked.Entity;
        }
        return await db.Purchases.Include(x => x.Requests).SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public Task<PoolPurchase?> FindAsync(Guid userId, string key, CancellationToken ct) =>
        db.Purchases.Include(x => x.Requests).SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);
    public Task<PoolPurchase?> FindByCodeAsync(string hash, CancellationToken ct) =>
        db.Purchases.Include(x => x.Requests).SingleOrDefaultAsync(x => x.Requests.Any(r => r.CodeHash == hash), ct);
    public Task<PoolRequest?> FindRedemptionAsync(Guid userId, string key, CancellationToken ct) =>
        db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.RedeemedBy == userId && x.RedemptionKey == key, ct);
    public async Task AddAsync(PoolPurchase purchase, CancellationToken ct) { db.Purchases.Add(purchase); await db.SaveChangesAsync(ct); }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<IReadOnlyList<Guid>> RecoveryIdsAsync(DateTimeOffset? after, Guid? afterId, CancellationToken ct) =>
        await db.Purchases.AsNoTracking().Where(x => x.Status == PoolPurchaseStatus.AwaitingPayment || x.Status == PoolPurchaseStatus.CancellationPending || x.Status == PoolPurchaseStatus.Paid && x.Requests.Any(r => r.VoucherStatus == PoolVoucherStatus.Issued && r.EndsAtUtc <= clock.GetUtcNow()))
            .Where(x => !after.HasValue || x.CreatedAt > after || x.CreatedAt == after && x.Id.CompareTo(afterId!.Value) > 0)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id).Take(50).ToArrayAsync(ct);
}

public sealed class PoolCatalogRepository(PoolsDbContext db) : IPoolCatalogRepository
{
    public Task<PoolCatalogEntry?> PoolAsync(Guid id, CancellationToken ct) => db.Venues.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PoolSessionEntry?> SessionAsync(Guid id, CancellationToken ct) => db.Sessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PoolCatalogEntry>> ProviderPoolsAsync(string provider, CancellationToken ct) =>
        await db.Venues.Where(x => x.ProviderKey == provider).ToArrayAsync(ct);
    public async Task<IReadOnlyList<PoolSessionEntry>> PoolSessionsAsync(Guid poolId, CancellationToken ct) =>
        await db.Sessions.Where(x => x.PoolId == poolId).ToArrayAsync(ct);
    public async Task AddPoolAsync(PoolCatalogEntry pool, CancellationToken ct) { await db.Venues.AddAsync(pool, ct); }
    public async Task AddSessionAsync(PoolSessionEntry session, CancellationToken ct) { await db.Sessions.AddAsync(session, ct); }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}

public sealed class PoolCatalogReader(PoolsDbContext db, IOptionsMonitor<PoolsOptions> options, TimeProvider clock) : IPoolCatalogReader
{
    private IQueryable<PoolCatalogEntry> Venues(PoolCatalogFilter f) => db.Venues.AsNoTracking().Where(x => x.IsActive)
        .Where(x => f.Search == null || x.Name.Contains(f.Search))
        .Where(x => f.State == null || x.State == f.State).Where(x => f.City == null || x.City == f.City)
        .Where(x => f.Gender == null || x.Gender == f.Gender || x.Gender == 2);
    internal static PoolVenueDto Venue(PoolCatalogEntry p) => new(p.Id, p.Name, p.State, p.City, p.Area, p.ImageUrl,
        p.Gender, p.Rating, p.MinimumSaleMinor, p.IsSelling, p.FetchedAt);
    private PoolSessionDto Session(PoolSessionEntry s, PoolCatalogEntry p) => new(s.Id, p.Id, p.Name, p.State, p.City,
        p.ImageUrl, s.Name, s.Gender, s.Date, s.Time, s.StartsAtUtc, s.EndsAtUtc, s.AdultMinor, s.ChildMinor,
        s.AdultEnabled, s.ChildEnabled, s.IsSelling && p.IsSelling,
        options.CurrentValue.PurchaseEnabled && p.IsActive && p.IsSelling && s.IsActive && s.IsSelling
            && s.StartsAtUtc > clock.GetUtcNow() && s.EndsAtUtc > s.StartsAtUtc,
        s.DescriptionHtml, s.TermsHtml, s.FetchedAt);

    public async Task<PoolPage<PoolVenueDto>> VenuesAsync(PoolCatalogFilter f, CancellationToken ct)
    {
        var query = Venues(f); var total = await query.CountAsync(ct);
        var sorted = f.Sort switch { "price" => query.OrderBy(x => x.MinimumSaleMinor), "rating" => query.OrderByDescending(x => x.Rating), _ => query.OrderBy(x => x.Name) };
        var rows = await sorted.ThenBy(x => x.Id).Skip(checked((f.Page - 1) * f.Limit)).Take(f.Limit).ToArrayAsync(ct);
        return new(rows.Select(Venue).ToArray(), f.Page, f.Limit, total);
    }
    public async Task<PoolPage<PoolSessionDto>> SessionsAsync(PoolCatalogFilter f, CancellationToken ct)
    {
        var query = from s in db.Sessions.AsNoTracking() join p in Venues(f with { Gender = null }) on s.PoolId equals p.Id
            where s.IsActive && (f.PoolId == null || s.PoolId == f.PoolId) && (f.Gender == null || s.Gender == f.Gender || s.Gender == 2)
            select new { S = s, P = p };
        if (f.Date.HasValue)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(f.Date.Value.ToDateTime(TimeOnly.MinValue), zone));
            var end = start.AddDays(1); query = query.Where(x => x.S.StartsAtUtc >= start && x.S.StartsAtUtc < end);
        }
        var total = await query.CountAsync(ct);
        var sorted = f.Sort switch { "price" => query.OrderBy(x => x.S.AdultEnabled ? x.S.AdultMinor : x.S.ChildMinor),
            "date" => query.OrderBy(x => x.S.StartsAtUtc), "rating" => query.OrderByDescending(x => x.P.Rating), _ => query.OrderBy(x => x.P.Name) };
        var rows = await sorted.ThenBy(x => x.S.Id).Skip(checked((f.Page - 1) * f.Limit)).Take(f.Limit).ToArrayAsync(ct);
        return new(rows.Select(x => Session(x.S, x.P)).ToArray(), f.Page, f.Limit, total);
    }
    public async Task<PoolVenueDetailsDto?> VenueAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Venues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct); if (p == null) return null;
        var rows = await db.Sessions.AsNoTracking().Where(x => x.PoolId == id && x.IsActive).OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
        return new(Venue(p), p.TermsHtml, rows.Select(s => Session(s, p)).ToArray());
    }
    public async Task<PoolSessionDto?> SessionAsync(Guid id, CancellationToken ct)
    {
        var s = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (s == null) return null;
        var p = await db.Venues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == s.PoolId && x.IsActive, ct);
        return p == null ? null : Session(s, p);
    }
    public async Task<IReadOnlyList<PoolLocationDto>> LocationsAsync(string? state, bool cities, CancellationToken ct)
    {
        var query = db.Venues.AsNoTracking().Where(x => x.IsActive && (state == null || x.State == state));
        var names = await (cities ? query.Select(x => x.City) : query.Select(x => x.State)).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        return names.Select(x => new PoolLocationDto(x, x)).ToArray();
    }
}
