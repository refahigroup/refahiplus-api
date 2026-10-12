using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;
public sealed class PoolBookingRepository(PoolsDbContext db) : IPoolBookingRepository
{
    public async Task<PoolBooking?> GetAsync(Guid id, CancellationToken ct)
    {
        var entry = db.ChangeTracker.Entries<PoolBooking>().SingleOrDefault(x => x.Entity.Id == id);
        if (entry != null) { await entry.ReloadAsync(ct); await entry.Collection(x => x.Attempts).LoadAsync(ct); return entry.Entity; }
        return await db.Bookings.Include(x => x.Attempts).SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public Task<PoolBooking?> FindAsync(Guid user, string key, CancellationToken ct) => db.Bookings.Include(x => x.Attempts).SingleOrDefaultAsync(x => x.UserId == user && x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<Guid>> CandidatesAsync(DateTimeOffset? afterCreated, Guid? afterId, CancellationToken ct)
        => await db.Bookings.AsNoTracking().Where(x => x.PaymentStatus != "Refunded" && !x.OrderCancellationCompleted
            && (x.CancellationStatus != "Cancelled" || x.OrderId != null) && (x.IssuanceStatus != "Issued" || x.CancellationStatus != "None")
            && x.ReservationStatus != "Failed"
            && (!afterCreated.HasValue || x.CreatedAt > afterCreated || x.CreatedAt == afterCreated && x.Id.CompareTo(afterId!.Value) > 0))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id).Take(50).ToArrayAsync(ct);
    public async Task AddAsync(PoolBooking b, CancellationToken ct) { db.Bookings.Add(b); await db.SaveChangesAsync(ct); }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
public sealed class PoolCatalogStore(PoolsDbContext db, IPoolMutationLock gate) : IPoolCatalogStore
{
    public async Task<PoolSummary?> GetAsync(string provider, string id, CancellationToken ct)
    {
        var s = await db.Catalog.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderKey == provider && x.ExternalId == id, ct);
        return s == null ? null : JsonSerializer.Deserialize<PoolSummary>(s.Json);
    }
    public async Task UpsertAsync(IReadOnlyList<PoolSummary> pools, CancellationToken ct)
    {
        // Serialize refreshes per provider across hosts; these snapshots never authorize a purchase.
        foreach (var group in pools.GroupBy(x => x.ProviderKey))
        {
            var lockId = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("catalog:" + group.Key))[..16]);
            await using var lease = await gate.AcquireAsync(lockId, ct);
            foreach (var item in group.DistinctBy(x => x.Id))
            {
                var existing = await db.Catalog.FindAsync([item.ProviderKey, item.Id], ct);
                if (existing == null) db.Catalog.Add(PoolCatalogSnapshot.Create(item)); else existing.Update(item);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
public sealed class PostgresPoolMutationLock(string connectionString) : IPoolMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            using var command = new NpgsqlCommand("select pg_advisory_lock(1347374924, hashtext(@id));", connection);
            command.Parameters.AddWithValue("id", id.ToString()); await command.ExecuteNonQueryAsync(ct);
            return new Handle(connection, id);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class Handle(NpgsqlConnection connection, Guid id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { using var command = new NpgsqlCommand("select pg_advisory_unlock(1347374924, hashtext(@id));", connection);
                command.Parameters.AddWithValue("id", id.ToString()); await command.ExecuteNonQueryAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
