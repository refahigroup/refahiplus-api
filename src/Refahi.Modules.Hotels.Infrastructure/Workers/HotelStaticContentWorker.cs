using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelStaticContentWorker(IServiceScopeFactory scopes, IOptions<HotelStaticContentOptions> options,
    ILogger<HotelStaticContentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            if (!options.Value.WorkerEnabled) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<HotelStaticContentSynchronizer>().RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Hotel static content sweep failed ErrorType={ErrorType}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

public sealed class HotelStaticContentSynchronizer(HotelsDbContext db, ISender sender, IHotelCatalogueRepository listings,
    HotelDiscoverySynchronizer discovery, SnappTripApiClient snapp, LamasooApiClient lamaso, IPersistentExternalMedia media,
    IOptions<HotelStaticContentOptions> options, ILogger<HotelStaticContentSynchronizer> logger)
{
    private static readonly Meter Meter = new("Refahi.Hotels.StaticContent");
    private static readonly Counter<long> SectionResults = Meter.CreateCounter<long>("hotel_content.sections");
    private static readonly Histogram<double> SectionDuration = Meter.CreateHistogram<double>("hotel_content.duration", "ms");
    private HotelStaticContentOptions Policy => options.Value;
    private static readonly JsonSerializerOptions Json = HotelLocalContentReader.Json;
    private static string Text(string? value) => HotelDiscoveryText.Plain(value);
    private async Task<T> Request<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(Policy.RequestTimeoutSeconds));
        return await action(deadline.Token);
    }
    public async Task RunAsync(CancellationToken ct)
    {
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.ToDictionary(x => x.Id);
        foreach (var provider in new[] { "SnappTrip", "Lamasoo" })
        {
            await using var providerLease = await listings.LockAsync($"static-provider:{provider}", ct);
            var mappings = provider == "SnappTrip"
                ? (await db.Set<SnappTripCityMapping>().AsNoTracking().ToArrayAsync(ct)).Select(x => (Native: x.ProviderCityId, City: x.CityId)).ToArray()
                : (await db.Set<LamasoCityMapping>().AsNoTracking().Where(x => x.IsEnabled).ToArrayAsync(ct)).Select(x => (Native: x.ProviderCityId, City: x.CityId)).ToArray();
            foreach (var mapping in mappings)
            {
                if (!cities.TryGetValue(mapping.City, out var city) || Policy.SyncScopes.Length > 0 && !Policy.SyncScopes.Contains($"{provider}:{city.Id}", StringComparer.OrdinalIgnoreCase)) continue;
                await using var lease = await listings.LockAsync($"discovery:{provider}:{mapping.Native}", ct);
                HotelDiscoverySyncState? state = provider == "SnappTrip" ? await db.Set<SnappTripDiscoverySyncState>().FindAsync([mapping.Native], ct)
                    : await db.Set<LamasoDiscoverySyncState>().FindAsync([mapping.Native], ct);
                if (state?.NextContentAttemptAtUtc > DateTimeOffset.UtcNow) { db.ChangeTracker.Clear(); continue; }
                state ??= provider == "SnappTrip" ? SnappTripDiscoverySyncState.Create(mapping.Native) : LamasoDiscoverySyncState.Create(mapping.Native);
                var succeeded = true;
                try
                {
                    // Full inventory validation precedes any retirement. Never infer deletion from a failed page.
                    var snappDetails = new Dictionary<string, SnappTripHotelDetail>();
                    var lamasoDetails = new Dictionary<string, LamasooHotel>();
                    var hotels = await discovery.Fetch(provider, mapping.Native, ct,
                        x => snappDetails.Add(x.id.ToString(), x), x => lamasoDetails.Add(x.Id, x), Policy.RequestTimeoutSeconds);
                    var nativeIds = hotels.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                    foreach (var hotel in hotels)
                    {
                        HotelDiscovery? row = provider == "SnappTrip"
                            ? await db.Set<SnappTripDiscoveryHotel>().SingleOrDefaultAsync(x => x.NativeId == hotel.Id, ct)
                            : await db.Set<LamasoDiscoveryHotel>().SingleOrDefaultAsync(x => x.NativeId == hotel.Id, ct);
                        var content = await FetchContent(provider, hotel, row is null ? null : HotelLocalContentReader.Content(row), ct,
                            snappDetails.GetValueOrDefault(hotel.Id), lamasoDetails.GetValueOrDefault(hotel.Id));
                        succeeded &= content.IsReady;
                        await using var identity = await listings.LockAsync("catalogue", ct);
                        await using var tx = await db.Database.BeginTransactionAsync(ct);
                        var listing = await listings.ListingAsync(city.Id, provider, hotel.Id, mapping.Native, ct);
                        if (row is null) { row = provider == "SnappTrip" ? SnappTripDiscoveryHotel.Create(listing.HotelKey, hotel.Id, mapping.Native) : LamasoDiscoveryHotel.Create(listing.HotelKey, hotel.Id, mapping.Native); db.Add(row); }
                        row.Publish(city.Id, Text(hotel.Name), Text(hotel.Description), Text(hotel.Address), hotel.Stars,
                            JsonSerializer.Serialize(hotel.Images), JsonSerializer.Serialize(content.Facilities.Select(x => x.Title)), content.Policies.Cancellation,
                            CatalogueText.Normalize(string.Join(" ", hotel.Name, content.EnglishName, "هتل", content.AccommodationTitle, city.Name, city.NameEn, city.ProvinceName, hotel.Address, HotelDiscoveryText.Summary(hotel.Description), provider, hotel.Stars > 0 ? $"{hotel.Stars} ستاره" : "", string.Join(" ", content.Facilities.Select(x => x.Title)))),
                            CatalogueText.Normalize(hotel.Name), DateTimeOffset.UtcNow, hotel.Latitude, hotel.Longitude);
                        row.UpdateContent(JsonSerializer.Serialize(content, Json), content.AccommodationType, content.Rating,
                            content.Facilities.Select(x => CatalogueText.Normalize(x.Title)).Distinct().ToArray(), content.IsReady);
                        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    }
                    await using (var identity = await listings.LockAsync("catalogue", ct))
                    {
                        var old = provider == "SnappTrip" ? (await db.Set<SnappTripDiscoveryHotel>().Where(x => x.NativeCityId == mapping.Native).ToArrayAsync(ct)).Cast<HotelDiscovery>().ToArray()
                            : (await db.Set<LamasoDiscoveryHotel>().Where(x => x.NativeCityId == mapping.Native).ToArrayAsync(ct)).Cast<HotelDiscovery>().ToArray();
                        foreach (var row in old.Where(x => !nativeIds.Contains(x.NativeId))) row.Retire();
                        if (db.Entry(state).State == EntityState.Detached) db.Add(state);
                        state.ScheduleContent(DateTimeOffset.UtcNow, succeeded, Policy.RefreshHours, Policy.RetryMinutes);
                        // A successful empty inventory is also initialized discovery coverage.
                        state.Succeed(DateTimeOffset.UtcNow, Policy.RefreshHours);
                        await db.SaveChangesAsync(ct);
                    }
                    logger.LogInformation("Hotel static content synchronized Provider={Provider} City={City} Hotels={Count} Ready={Ready}", provider, city.Id, hotels.Count, succeeded);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    db.ChangeTracker.Clear();
                    state = provider == "SnappTrip" ? await db.Set<SnappTripDiscoverySyncState>().FindAsync([mapping.Native], ct) : await db.Set<LamasoDiscoverySyncState>().FindAsync([mapping.Native], ct);
                    if (state is null) { state = provider == "SnappTrip" ? SnappTripDiscoverySyncState.Create(mapping.Native) : LamasoDiscoverySyncState.Create(mapping.Native); db.Add(state); }
                    state.ScheduleContent(DateTimeOffset.UtcNow, false, Policy.RefreshHours, Policy.RetryMinutes); await db.SaveChangesAsync(ct);
                    logger.LogWarning("Hotel static content failed Provider={Provider} City={City} ErrorType={ErrorType}", provider, city.Id, ex.GetType().Name);
                }
                db.ChangeTracker.Clear();
            }
        }
    }

    internal async Task<HotelStaticContent> FetchContent(string provider, ProviderHotel hotel, HotelStaticContent? old, CancellationToken ct,
        SnappTripHotelDetail? snappDetail = null, LamasooHotel? lamasoDetail = null)
    {
        var sections = old?.Sections.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, HotelContentSection>();
        var fetchedDetail = snappDetail;
        var content = old ?? new HotelStaticContent(hotel.EnglishName, hotel.AccommodationType, null, new(), [], [], [], [], null, null, sections);
        async Task Apply(string key, Func<Task> fetch)
        {
            if (sections.TryGetValue(key, out var previous) && previous.Status is "ready" or "unsupported" && previous.NextAttemptAtUtc > DateTimeOffset.UtcNow) return;
            var watch = Stopwatch.StartNew();
            try { await fetch(); sections[key] = new("ready", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(Policy.RefreshHours)); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                sections[key] = new("failed", previous?.LastSuccessAtUtc, DateTimeOffset.UtcNow.AddMinutes(Policy.RetryMinutes), ex.GetType().Name);
                logger.LogWarning("Hotel content section failed Provider={Provider} Section={Section} ErrorType={ErrorType}", provider, key, ex.GetType().Name);
            }
            finally
            {
                var tags = new TagList { { "provider", provider }, { "section", key }, { "status", sections.GetValueOrDefault(key)?.Status ?? "cancelled" } };
                SectionResults.Add(1, tags); SectionDuration.Record(watch.Elapsed.TotalMilliseconds, tags);
            }
        }
        if (provider == "SnappTrip")
        {
            var id = long.Parse(hotel.Id);
            await Apply("profile", async () => {
                var detail = snappDetail ?? (await Request(t => snapp.GetHotelsDetailsAsync([id], t), ct)).Single().hotel;
                fetchedDetail = detail;
                if (detail.id != id || detail.city.id.ToString() != hotel.CityId || string.IsNullOrWhiteSpace(detail.title)) throw new InvalidOperationException("اطلاعات هتل معتبر نیست.");
                content = content with { EnglishName = Text(detail.title_en), AccommodationType = Text(detail.accommodation_type), AccommodationTitle = Text(detail.accommodation_title),
                    Policies = new(Text(detail.policies.check_in_time), Text(detail.policies.check_out_time), detail.policies.child_age, detail.policies.infant_age, detail.policies.foreigners_fee, Text(detail.policies.cancellation)),
                    Rating = detail.reviews.ratings.HasValue && float.IsFinite(detail.reviews.ratings.Value) && detail.reviews.ratings is >= 0 and <= 10 ? detail.reviews.ratings : null,
                    ReviewCount = detail.reviews.reviews is >= 0 ? detail.reviews.reviews : null };
            });
            await Apply("facilities", async () => {
                var response = (await Request(t => snapp.GetHotelsFacilitiesAsync([id], t), ct)).ToArray();
                if (response.Length != 1) throw new InvalidOperationException("امکانات هتل ناقص است.");
                content = content with { Facilities = response[0].facilities.Select(x => new HotelFacility(Text(x.title), Text(x.icon))).ToArray() };
            });
            await Apply("rooms", async () => {
                var response = (await Request(t => snapp.GetHotelsRoomsAsync([id], t), ct)).Single();
                if (response.hotel_id != id || response.rooms.Any(x => x.hotel_id != id || x.id <= 0 || x.adults < 0 || x.children < 0 || x.extra_bed < 0)
                    || response.rooms.Select(x => x.id).Distinct().Count() != response.rooms.Count) throw new InvalidOperationException("اطلاعات اتاق معتبر نیست.");
                content = content with { Rooms = response.rooms.Select(x => new HotelStaticRoom(x.id.ToString(), Text(x.title), Text(x.description), x.adults, x.children, x.extra_bed, Text(x.board_type),
                    x.facilities_tags.SelectMany(f => f.facilities).Select(f => new HotelFacility(Text(f.title), Text(f.icon))).ToArray(), AccommodationType: Text(x.accommodation_type))).ToArray() };
            });
            await Apply("reviews", async () => {
                if (Policy.SnappTripPublishedReviewStatuses.Length == 0) throw new InvalidOperationException("وضعیت انتشار نظرات هنوز تأیید نشده است.");
                var response = (await Request(t => snapp.GetHotelsReviewsAsync([id], t), ct)).Single();
                if (response.hotel_id != id || response.reviews.Any(x => x.hotel_id != id || x.id <= 0) || response.reviews.Select(x => x.id).Distinct().Count() != response.reviews.Count)
                    throw new InvalidOperationException("فهرست نظرات معتبر نیست.");
                content = content with { Reviews = response.reviews.Where(x => Policy.SnappTripPublishedReviewStatuses.Contains(x.status, StringComparer.OrdinalIgnoreCase)
                    && double.IsFinite(x.rate_overall) && x.rate_overall is >= 0 and <= 10).Select(MapReview).ToArray() };
            });
            await Apply("gallery", async () => {
                var response = (await Request(t => snapp.GetHotelsGalleriesAsync([id], t), ct)).Single();
                if (response.hotel_id != id) throw new InvalidOperationException("گالری معتبر نیست.");
                var images = hotel.Images.Select((url, index) => (Url: url, Title: url == fetchedDetail?.cover.url ? Text(fetchedDetail.cover.title) : "", Description: url == fetchedDetail?.cover.url ? Text(fetchedDetail.cover.description) : "", Cover: index == 0))
                    .Concat(response.gallery.Select(x => (Url: x.url, Title: Text(x.title), Description: Text(x.description), Cover: false)))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Url)).GroupBy(x => x.Url, StringComparer.Ordinal).Select(x => x.OrderByDescending(i => i.Title.Length + i.Description.Length).First()).ToArray();
                var gallery = new List<HotelStaticImage>();
                foreach (var image in images) gallery.Add(new(await media.EnsureAsync(image.Url, new("Hotels", provider), ct), image.Title, image.Description, image.Url == hotel.Images.FirstOrDefault(), image.Url));
                content = content with { Gallery = gallery };
            });
        }
        else
        {
            await Apply("profile", () => { content = content with { EnglishName = null, AccommodationType = null }; return Task.CompletedTask; });
            await Apply("rooms", async () => {
                var item = lamasoDetail ?? (await Request(t => lamaso.SendAsync<LamasooInventory>(HttpMethod.Get, "booking/hotel_inventory?city_id=" + Uri.EscapeDataString(hotel.CityId), null, false, t), ct)).Hotels.Single(x => x.Id == hotel.Id && x.City == hotel.CityId);
                if (item.RoomTypes.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) || x.Capacity <= 0 || x.ExtraCapacity < 0 || x.MaxChildrenWithoutBed < 0)
                    || item.RoomTypes.Select(x => x.Id).Distinct().Count() != item.RoomTypes.Count || item.ChildPolicies.Any(x => x.From < 0 || x.To < x.From)) throw new InvalidOperationException("اطلاعات اتاق معتبر نیست.");
                content = content with { Rooms = item.RoomTypes.Select(x => new HotelStaticRoom(x.Id, Text(x.Title), "", null, null, x.ExtraCapacity, null, [], Capacity: x.Capacity, MaxChildrenWithoutBed: x.MaxChildrenWithoutBed)).ToArray(),
                    Policies = content.Policies with { ChildPolicies = item.ChildPolicies.Select(x => new HotelChildPolicy(x.From, x.To, Text(x.Type), x.RequiresBed)).ToArray() },
                    RatePlans = item.RatePlans.Select(x => new HotelStaticRatePlan(x.Id, Text(x.MealType), x.AgencyProviderId)).ToArray() };
            });
            foreach (var key in new[] { "facilities", "reviews" }) sections[key] = new("unsupported", null, DateTimeOffset.MaxValue);
            await Apply("gallery", async () => {
                var gallery = new List<HotelStaticImage>();
                foreach (var url in hotel.Images) gallery.Add(new(await media.EnsureAsync(url, new("Hotels", provider), ct), "", "", gallery.Count == 0, url));
                content = content with { Gallery = gallery };
            });
        }
        return content with { Sections = sections, Version = (old?.Version ?? 0) + 1 };
    }
    internal static HotelStaticReview MapReview(SnappTripHotelReview x) => new(x.id.ToString(), Text(x.fullname), Text(x.comment), x.rate_overall,
        x.registered_date, x.update_date, x.recommended, x.has_ever_booked,
        new Dictionary<string, double> { ["clean"] = x.rate_clean, ["location"] = x.rate_location, ["staff"] = x.rate_staff, ["services"] = x.rate_services,
            ["sleepQuality"] = x.rate_sleep_quality, ["valueForMoney"] = x.rate_value_for_money, ["facility"] = x.rate_facility, ["foodQuality"] = x.rate_food_quality, ["collectiveAverage"] = x.rate_collective_avg }
            .Where(x => double.IsFinite(x.Value) && x.Value is >= 0 and <= 10).ToDictionary(x => x.Key, x => x.Value), Text(x.status));
}
