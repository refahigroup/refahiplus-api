using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;

public sealed class HotelCatalogueRepository(HotelsDbContext db) : IHotelCatalogueRepository
{
    public async Task<IReadOnlyList<HotelCityReference>> CitiesAsync(CancellationToken ct) => await db.Set<HotelCityReference>().ToListAsync(ct);
    public async Task<HotelListing> ListingAsync(int referenceCityId, string provider, string nativeId, string nativeCityId, CancellationToken ct)
    {
        var found = db.Set<HotelListing>().Local.FirstOrDefault(x => x.Provider == provider && x.NativeId == nativeId)
            ?? await db.Set<HotelListing>().SingleOrDefaultAsync(x => x.Provider == provider && x.NativeId == nativeId, ct);
        if (found is not null) { found.AttachReferenceCity(referenceCityId); return found; }
        var listing = HotelListing.Create(referenceCityId, provider, nativeId, nativeCityId); db.Add(listing); return listing;
    }
    public Task<HotelListing?> FindListingAsync(Guid key, CancellationToken ct) => db.Set<HotelListing>().SingleOrDefaultAsync(x => x.HotelKey == key, ct);
    public Task<HotelOfferSnapshot?> OfferAsync(string token, CancellationToken ct) => db.Set<HotelOfferSnapshot>().SingleOrDefaultAsync(x => x.Token == token, ct);
    public async Task<HotelExternalReservation?> ReservationAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<HotelExternalReservation>().SingleOrDefaultAsync(x => x.HotelRequestId == id, ct);
        if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public Task<IReadOnlyList<HotelExternalReservation>> RecoveryAsync(CancellationToken ct) => RecoveryAsync(false, ct);
    public async Task<IReadOnlyList<HotelExternalReservation>> RecoveryAsync(bool reconciliationOnly, CancellationToken ct) => await db.Set<HotelExternalReservation>()
        .AsNoTracking().Where(x => x.NextRecoveryAtUtc <= DateTime.UtcNow &&
            (reconciliationOnly ? x.State == HotelExternalState.Unknown :
                x.State == HotelExternalState.Prepared || x.State == HotelExternalState.Submitting || x.State == HotelExternalState.Reserved
                || x.State == HotelExternalState.Confirming || (x.NoExternalLiability && x.NextRecoveryAtUtc != DateTime.MaxValue)))
        .OrderByDescending(x => db.HotelRequests.Any(r => r.Id == x.HotelRequestId && r.OrderId != null))
        .ThenBy(x => x.NextRecoveryAtUtc).ThenBy(x => x.UpdatedAtUtc).Take(100).ToListAsync(ct);
    public async Task<IReadOnlyList<HotelExternalOperation>> OperationsAsync(Guid id, CancellationToken ct) =>
        await db.Set<HotelExternalOperation>().Where(x => x.HotelRequestId == id).OrderBy(x => x.StartedAtUtc).ToListAsync(ct);
    public void AddOperation(HotelExternalOperation operation) => db.Add(operation);
    public void AddCity(HotelCityReference value) => db.Add(value);
    public void AddOffer(HotelOfferSnapshot value) => db.Add(value);
    public void AddReservation(HotelExternalReservation value) => db.Add(value);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    // A dedicated connection holds a cross-process advisory lock without an open EF transaction during HTTP calls.
    public async Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct)
    {
        var connection = new Npgsql.NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync(ct);
        try
        {
            await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", connection);
            command.Parameters.AddWithValue("key", "hotels:" + key); await command.ExecuteNonQueryAsync(ct); return new AdvisoryLease(connection, "hotels:" + key);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class AdvisoryLease(Npgsql.NpgsqlConnection connection, string key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@key, 0))", connection);
                command.Parameters.AddWithValue("key", key); await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
