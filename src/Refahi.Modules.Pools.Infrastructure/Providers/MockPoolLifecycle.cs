using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Infrastructure.Persistence;

namespace Refahi.Modules.Pools.Infrastructure.Providers;
public sealed class MockPoolOptions { public string Scenario { get; set; } = "Success"; }
public sealed class MockPoolLifecycle(PoolsDbContext db, IPoolMutationLock gate, IPoolPurchasePolicy policy,
    IOptions<PoolsOptions> options, IOptions<MockPoolOptions> mock)
{
    public async Task<PoolReservation> ReserveAsync(string provider, string operation, PoolSelection selection, PoolQuote quote, CancellationToken ct)
    {
        policy.EnsureAllowed(true);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { selection, quote.TotalMinor }))));
        var reference = $"MOCK-{provider}-{operation}";
        await using var lease = await gate.AcquireAsync(Lock(reference), ct);
        var existing = await db.MockReservations.FindAsync([reference], ct);
        if (existing != null)
        { if (existing.Fingerprint != fingerprint) throw new PoolException("کلید رزرو آزمایشی تکراری است"); return existing.View(); }
        if (mock.Value.Scenario == "Capacity") throw new PoolException("ظرفیت کافی نیست");
        var r = MockPoolReservation.Create(provider, operation, fingerprint, quote.TotalMinor ?? throw new PoolException("مبلغ ریالی مشخص نیست"),
            DateTimeOffset.UtcNow.AddMinutes(options.Value.ReservationMinutes));
        db.MockReservations.Add(r); await db.SaveChangesAsync(ct);
        if (mock.Value.Scenario == "ReserveAmbiguous") throw new PoolAmbiguousException();
        return r.View();
    }
    public async Task<PoolReservation?> StatusAsync(string reference, CancellationToken ct)
    { policy.EnsureAllowed(true); var r = await db.MockReservations.AsNoTracking().SingleOrDefaultAsync(x => x.Reference == reference, ct); return r?.View(); }
    public async Task<PoolReservation> MutateAsync(string reference, bool cancel, CancellationToken ct)
    {
        policy.EnsureAllowed(true); await using var lease = await gate.AcquireAsync(Lock(reference), ct);
        var r = await db.MockReservations.SingleOrDefaultAsync(x => x.Reference == reference, ct) ?? throw new PoolException("رزرو آزمایشی یافت نشد", 404);
        await db.Entry(r).ReloadAsync(ct);
        if (!cancel && mock.Value.Scenario == "ConfirmFailure") throw new PoolException("صدور آزمایشی ناموفق بود");
        if (cancel) r.Cancel(); else r.Confirm();
        await db.SaveChangesAsync(ct);
        if (mock.Value.Scenario == (cancel ? "CancelAmbiguous" : "ConfirmAmbiguous")) throw new PoolAmbiguousException();
        return r.View();
    }
    private static Guid Lock(string reference) => new(SHA256.HashData(Encoding.UTF8.GetBytes("mock:" + reference))[..16]);
}
public sealed class PoolProviderWithMock(IPoolProvider discovery, MockPoolLifecycle lifecycle, IOptions<PoolsOptions> options) :
    IPoolProvider, IPoolReservationProvider, IPoolConfirmationProvider, IPoolStatusProvider, IPoolCancellationProvider
{
    public string Key => discovery.Key;
    public string Name => discovery.Name;
    public PoolCapabilities Capabilities => new(true, discovery.Capabilities.Quote, discovery.Capabilities.Quote && options.Value.PurchaseEnabled && options.Value.MockLifecycleEnabled,
        options.Value.MockLifecycleEnabled, options.Value.MockLifecycleEnabled, options.Value.MockLifecycleEnabled, options.Value.MockLifecycleEnabled);
    public Task<IReadOnlyList<PoolState>> StatesAsync(bool active, CancellationToken ct) => discovery.StatesAsync(active, ct);
    public Task<IReadOnlyList<PoolCity>> CitiesAsync(string? state, bool active, CancellationToken ct) => discovery.CitiesAsync(state, active, ct);
    public Task<PoolCatalog> SearchAsync(PoolSearchQuery query, CancellationToken ct) => discovery.SearchAsync(query, ct);
    public Task<IReadOnlyList<PoolSession>> SessionsAsync(string id, CancellationToken ct) => discovery.SessionsAsync(id, ct);
    public Task<string> TermsAsync(string id, CancellationToken ct) => discovery.TermsAsync(id, ct);
    public Task<PoolQuote> QuoteAsync(PoolSelection selection, CancellationToken ct) => discovery.QuoteAsync(selection, ct);
    public Task<PoolReservation> ReserveAsync(string op, PoolSelection s, PoolQuote q, CancellationToken ct) => lifecycle.ReserveAsync(Key, op, s, q, ct);
    public Task<PoolReservation> ConfirmAsync(string reference, string op, CancellationToken ct) => lifecycle.MutateAsync(reference, false, ct);
    public Task<PoolReservation?> StatusAsync(string reference, CancellationToken ct) => lifecycle.StatusAsync(reference, ct);
    public Task<PoolReservation?> FindByOperationAsync(string operationId, CancellationToken ct) => lifecycle.StatusAsync($"MOCK-{Key}-{operationId}", ct);
    public Task<PoolReservation> CancelAsync(string reference, string op, CancellationToken ct) => lifecycle.MutateAsync(reference, true, ct);
}
