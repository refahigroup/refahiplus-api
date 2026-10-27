using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Catalogue.Application.Contracts;
namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelDiscoveryWorker(IServiceScopeFactory scopes, ILogger<HotelDiscoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (scope.ServiceProvider.GetRequiredService<IOptions<HotelDiscoveryOptions>>().Value.WorkerEnabled)
                    await scope.ServiceProvider.GetRequiredService<HotelDiscoverySynchronizer>().RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Hotel discovery sweep failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
public sealed class HotelDiscoverySynchronizer(HotelsDbContext db, ISender sender, IHotelCatalogueProviderRegistry providers, IHotelCatalogueRepository listings, SnappTripApiClient snapp, LamasooApiClient lamaso, IOptions<HotelDiscoveryOptions> options, ILogger<HotelDiscoverySynchronizer> logger,
    IOptions<HotelStaticContentOptions>? staticOptions = null)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities;
        foreach (var provider in providers.Active)
        {
            var mappings = provider.Name == "SnappTrip" ? (await db.Set<SnappTripCityMapping>().AsNoTracking().ToListAsync(ct)).Select(x => (Native: x.ProviderCityId, City: x.CityId)).ToArray() : (await db.Set<LamasoCityMapping>().AsNoTracking().Where(x => x.IsEnabled).ToListAsync(ct)).Select(x => (Native: x.ProviderCityId, City: x.CityId)).ToArray();
            foreach (var mapping in mappings)
            {
                var city = cities.FirstOrDefault(x => x.Id == mapping.City); if (city is null) continue;
                if (staticOptions?.Value.WorkerEnabled == true && (staticOptions.Value.SyncScopes.Length == 0 || staticOptions.Value.SyncScopes.Contains($"{provider.Name}:{city.Id}", StringComparer.OrdinalIgnoreCase))) continue;
                await using var lease = await listings.LockAsync($"discovery:{provider.Name}:{mapping.Native}", ct);
                HotelDiscoverySyncState? state = provider.Name == "SnappTrip" ? await db.Set<SnappTripDiscoverySyncState>().FindAsync([mapping.Native], ct) : await db.Set<LamasoDiscoverySyncState>().FindAsync([mapping.Native], ct);
                if (state?.NextAttemptAtUtc > DateTimeOffset.UtcNow) continue;
                state ??= provider.Name == "SnappTrip" ? SnappTripDiscoverySyncState.Create(mapping.Native) : LamasoDiscoverySyncState.Create(mapping.Native);
                try
                {
                    var hotels = await Fetch(provider.Name, mapping.Native, ct);
                    await using var identityLock = await listings.LockAsync("catalogue", ct);
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var old = provider.Name == "SnappTrip" ? (await db.Set<SnappTripDiscoveryHotel>().Where(x => x.NativeCityId == mapping.Native).ToListAsync(ct)).Cast<HotelDiscovery>().ToList() : (await db.Set<LamasoDiscoveryHotel>().Where(x => x.NativeCityId == mapping.Native).ToListAsync(ct)).Cast<HotelDiscovery>().ToList();
                    foreach (var row in old) row.Retire();
                    foreach (var hotel in hotels)
                    {
                        var listing = await listings.ListingAsync(city.Id, provider.Name, hotel.Id, mapping.Native, ct);
                        var row = old.FirstOrDefault(x => x.NativeId == hotel.Id);
                        if (row is null) { row = provider.Name == "SnappTrip" ? SnappTripDiscoveryHotel.Create(listing.HotelKey, hotel.Id, mapping.Native) : LamasoDiscoveryHotel.Create(listing.HotelKey, hotel.Id, mapping.Native); db.Add(row); }
                        row.Publish(city.Id, HotelDiscoveryText.Plain(hotel.Name), HotelDiscoveryText.Plain(hotel.Description), HotelDiscoveryText.Plain(hotel.Address), hotel.Stars, JsonSerializer.Serialize(hotel.Images), JsonSerializer.Serialize(hotel.Facilities ?? []), hotel.Policies, CatalogueText.Normalize(string.Join(" ", HotelDiscoveryText.Plain(hotel.Name), hotel.EnglishName, "هتل", city.Name, city.NameEn, city.ProvinceName, HotelDiscoveryText.Plain(hotel.Address), HotelDiscoveryText.Summary(hotel.Description), provider.Name, hotel.Stars > 0 ? $"{hotel.Stars} ستاره" : "")), CatalogueText.Normalize(HotelDiscoveryText.Plain(hotel.Name)), DateTimeOffset.UtcNow, hotel.Latitude, hotel.Longitude);
                    }
                    if (db.Entry(state).State == EntityState.Detached) db.Add(state);
                    state.Succeed(DateTimeOffset.UtcNow, options.Value.RefreshHours);
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    logger.LogInformation("Hotel discovery published Provider={Provider} City={City} Count={Count}", provider.Name, city.Id, hotels.Count);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    db.ChangeTracker.Clear();
                    state = provider.Name == "SnappTrip" ? await db.Set<SnappTripDiscoverySyncState>().FindAsync([mapping.Native], ct) : await db.Set<LamasoDiscoverySyncState>().FindAsync([mapping.Native], ct);
                    if (state is null) { state = provider.Name == "SnappTrip" ? SnappTripDiscoverySyncState.Create(mapping.Native) : LamasoDiscoverySyncState.Create(mapping.Native); db.Add(state); }
                    state.Fail(DateTimeOffset.UtcNow, options.Value.RetryMinutes); await db.SaveChangesAsync(ct);
                    logger.LogWarning("Hotel discovery sync failed Provider={Provider} City={City} ErrorType={ErrorType}", provider.Name, city.Id, ex.GetType().Name);
                }
                db.ChangeTracker.Clear();
            }
        }
    }
    private async Task<T> Request<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct, int? timeoutSeconds = null) { using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? options.Value.RequestTimeoutSeconds)); return await action(deadline.Token); }
    internal async Task<IReadOnlyList<ProviderHotel>> Fetch(string provider, string city, CancellationToken ct,
        Action<Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract.SnappTripHotelDetail>? snappDetail = null,
        Action<LamasooHotel>? lamasoDetail = null, int? requestTimeoutSeconds = null)
    {
        if (provider == "Lamasoo")
        {
            var inventory = await Request(t => lamaso.SendAsync<LamasooInventory>(HttpMethod.Get, "booking/hotel_inventory?city_id=" + Uri.EscapeDataString(city), null, false, t), ct, requestTimeoutSeconds);
            var hotels = inventory.Hotels.Select(LamasooHotelMapper.Hotel).ToArray(); Validate(hotels, city);
            foreach (var item in inventory.Hotels) lamasoDetail?.Invoke(item);
            return hotels;
        }
        var ids = new HashSet<long>(); var ended = false;
        for (var page = 0; page < 1000; page++)
        {
            var batch = (await Request(t => snapp.GetCityHotelsAsync(long.Parse(city), 100, page * 100, t), ct, requestTimeoutSeconds)).ToArray();
            if (batch.Length == 0) { ended = true; break; }
            foreach (var row in batch) if (row.id <= 0 || row.city_id.ToString() != city || !ids.Add(row.id)) throw new InvalidOperationException("فهرست هتل معتبر نیست.");
        }
        if (!ended) throw new InvalidOperationException("سقف صفحات هتل رد شد.");
        var result = new List<ProviderHotel>();
        foreach (var batch in ids.Chunk(10))
        {
            var details = (await Request(t => snapp.GetHotelsDetailsAsync(batch, t), ct, requestTimeoutSeconds)).Select(x => x.hotel).ToArray();
            if (details.Any(x => x.city.id.ToString() != city) || details.Length != batch.Length || details.Select(x => x.id).Distinct().Count() != batch.Length || details.Any(x => !batch.Contains(x.id))) throw new InvalidOperationException("اطلاعات معرفی هتل ناقص است.");
            foreach (var detail in details) snappDetail?.Invoke(detail);
            foreach (var h in details.Where(x => x.enabled)) result.Add(new(h.id.ToString(), city, h.title, h.address ?? "", h.stars, new[] { h.cover.url }.Concat(h.gallery.Select(x => x.url)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray(), h.description ?? "", h.facilities.Select(x => x.title).ToArray(), null, h.accommodation_type, h.policies.check_in_time, h.policies.check_out_time, h.title_en, Coordinate(h.location)?.Latitude, Coordinate(h.location)?.Longitude));
        }
        Validate(result, city); return result;
    }
    private static CataloguePoint? Coordinate(Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract.SnappTripHotelDetailLocation? location)
    {
        if (location?.lat is not { } lat || location.lon is not { } lon || (lat == 0 && lon == 0)) return null;
        var point = new CataloguePoint((double)lat, (double)lon);
        return point.IsValid ? point : null;
    }
    private static void Validate(IEnumerable<ProviderHotel> hotels, string city) { var seen = new HashSet<string>(); foreach (var h in hotels) if (string.IsNullOrWhiteSpace(h.Id) || !seen.Add(h.Id) || h.CityId != city || string.IsNullOrWhiteSpace(h.Name) || h.Stars is < 0 or > 5) throw new InvalidOperationException("داده معرفی هتل معتبر نیست."); }
}
