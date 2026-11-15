using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Persistence;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;
using Refahi.Modules.Cinema.Infrastructure.Workers;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Cinema.Tests;

public sealed class CinemaDiscoveryTests
{
    private const string Film = "01ktm95cswzx3tgaqy1pett33e";
    private const string Homonym = "01ktm95cswzx3tgaqy1pett33f";
    private const string Venue = "01ktm9ahegwhvx5xs8n754aa63";
    private const string UnmappedVenue = "01ktm9ahegwhvx5xs8n754aa64";
    private const string Schedule = "01m4442930npemsr3a8c7nxfyt";
    private const string SecondSchedule = "01m4442930npemsr3a8c7nxfyu";
    private const string HomonymSchedule = "01m4442930npemsr3a8c7nxfyv";
    private const string Art = "01kyypwh33rwgsn02hapecvxcn";
    private const string Cinema = "01kycvfgfdpy45wp3h61ydhkbd";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Adapter_reads_all_pages_and_rejects_incomplete_pagination()
    {
        using var stub = new FeedHandler();
        using var client = new HttpClient(stub) { BaseAddress = new("https://example.test/api/v1/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = Provider(client, cache);

        var result = await provider.ReadDiscoveryAsync(Now.AddHours(9), 14, default);
        Assert.Equal(2, stub.ScheduleQueries.Count);
        Assert.All(stub.ScheduleQueries, query =>
            Assert.EndsWith("&starts_from=2026-11-17&starts_to=2026-12-01", query));
        Assert.Equal("علي نیک‌نام", Assert.Single(Assert.Single(result.Films).Credits!).Artist.Name);
        Assert.Equal(2, result.Sessions.Length);
        Assert.All(result.Sessions, x => Assert.Equal(TimeSpan.Zero, x.StartsAt.Offset));
        Assert.All(result.Venues, x => Assert.NotNull(x.Point));
        stub.BreakSecondPage = true;
        await Assert.ThrowsAsync<CinemaException>(() => provider.ReadDiscoveryAsync(Now, 14, default));
    }

    [CinemaPostgresFact]
    public async Task Publication_is_atomic_and_search_uses_canonical_locations_and_credits()
    {
        var connection = Environment.GetEnvironmentVariable("CINEMA_TEST_POSTGRES")!;
        var dbOptions = new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(connection).Options;
        var clock = new TestClock(Now);
        var options = Options.Create(new CinemaDiscoveryOptions());
        using var stub = new FeedHandler();
        using var client = new HttpClient(stub) { BaseAddress = new("https://example.test/api/v1/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = Provider(client, cache);
        var factory = new CinemaProviderFactory([provider]);
        await using var db = new CinemaDbContext(dbOptions);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE cinema.iticket_screenings, cinema.iticket_discovery_state, cinema.iticket_mappings,
                cinema.iticket_geographies, cinema.film_credits, cinema.films, cinema.venues, cinema.people;
            """);
        async Task SyncAsync()
        {
            await using var scope = new CinemaDbContext(dbOptions);
            await new ITicketDiscoverySync(scope, provider, new PostgresCinemaMutationLock(connection),
                new References(), new Media(), clock, options, NullLogger<ITicketDiscoverySync>.Instance).RunAsync(default);
        }
        var inventory = new ITicketScheduleInventory(db);
        var source = new CinemaCatalogueSource(db, [inventory], factory, clock, options);
        await Assert.ThrowsAsync<CatalogueUnavailableException>(() => source.SearchAsync(new(), 10, default));

        await Task.WhenAll(SyncAsync(), SyncAsync());
        var result = await source.SearchAsync(new(Q: "علی نیک نام"), 10, default);
        var film = Assert.Single(result.Items);
        Assert.Equal(2, film.Locations!.Count);
        Assert.Equal(1, result.TotalCount);
        Assert.False(result.IsPartial);
        Assert.Equal(101, Assert.Single(await source.CitiesAsync(default)).Id);
        Assert.Equal("علي نیک‌نام", Assert.Single((await source.ShowAsync(Guid.Parse(film.SourceItemId), default)).Artists));
        var city = Assert.Single((await source.SearchAsync(new(Q: "خیابان", CityId: 101, ProvinceId: 50), 10, default)).Items);
        Assert.Equal(101, Assert.Single(city.Locations!).CityId);
        Assert.EndsWith("?place=" + city.DisplayLocation!.Id, city.DetailPath);
        Assert.Empty((await source.SearchAsync(new(Q: "سینمای نامرتبط", CityId: 101), 10, default)).Items);
        var nearby = Assert.Single((await source.SearchAsync(new(Location: "nearby", Latitude: 35, Longitude: 51), 10, default)).Items);
        Assert.Equal(city.DisplayLocation.Id, nearby.DisplayLocation!.Id);

        var filmId = Guid.Parse(film.SourceItemId);
        var placeId = Guid.Parse(city.DisplayLocation.Id);
        var days = await source.PlacesAsync(filmId, null, null, default);
        Assert.Contains(days, x => x.Date == "2026-11-17" && x.Places.Any(p => p.Id == placeId.ToString("D")));
        var session = Assert.Single(await source.SessionsAsync(filmId, placeId, "2026-11-17", default));
        Assert.Equal(new CinemaScheduleReference("iticket", Schedule),
            await source.FindScheduleAsync(Guid.Parse(session.Id), default));

        var otherKey = Guid.NewGuid();
        var second = new SecondInventory(inventory, otherKey);
        var combined = new CinemaCatalogueSource(db, [inventory, second], factory, clock, options);
        var shared = Assert.Single((await combined.SearchAsync(new(Q: "نیک نام"), 10, default)).Items);
        Assert.Equal(2, shared.Locations!.Count);
        Assert.Equal(new CinemaScheduleReference("second", Schedule), await combined.FindScheduleAsync(otherKey, default));

        stub.BreakSecondPage = true;
        clock.Now = Now.AddMinutes(6);
        await SyncAsync();
        var partial = await source.SearchAsync(new(), 10, default);
        Assert.True(partial.IsPartial);
        Assert.True((await source.ReadCitiesAsync(default)).IsPartial);
        Assert.Null(partial.TotalCount);
        Assert.Equal(film.Id, Assert.Single(partial.Items).Id);

        stub.BreakSecondPage = false;
        stub.HideSecondSchedule = true;
        clock.Now = Now.AddMinutes(12);
        await SyncAsync();
        var refreshed = Assert.Single((await source.SearchAsync(new(), 10, default)).Items);
        Assert.Equal(film.Id, refreshed.Id);
        Assert.Single(refreshed.Locations!);
        Assert.Equal(session.Id, Assert.Single(await source.SessionsAsync(filmId, placeId, "2026-11-17", default)).Id);

        stub.IncludeHomonym = true;
        clock.Now = Now.AddMinutes(18);
        await SyncAsync();
        var homonyms = await source.SearchAsync(new(Q: "آزمایش فیلم"), 10, default);
        Assert.Equal(2, homonyms.Items.Select(x => x.Id).Distinct().Count());
        Assert.False(homonyms.IsPartial);

        stub.IncludeHomonym = false;
        clock.Now = Now.AddHours(2);
        await Assert.ThrowsAsync<CatalogueUnavailableException>(() => source.SearchAsync(new(), 10, default));
        clock.Now = Now.AddHours(10).AddMinutes(45);
        await SyncAsync();
        Assert.Single((await source.SearchAsync(new(), 10, default)).Items);
        clock.Now = clock.Now.AddMinutes(10);
        Assert.Empty((await source.SearchAsync(new(), 10, default)).Items);
    }

    private static ITicketCinemaProvider Provider(HttpClient client, IMemoryCache cache)
    {
        var connection = Options.Create(new iTicketOptions());
        var options = Options.Create(new CinemaProviderSettings { CinemaCategories = [Cinema], ArtCategories = [Art] });
        return new(new iTicketClient(client, connection), options, connection, cache, NullLogger<ITicketCinemaProvider>.Instance);
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        public List<string> ScheduleQueries { get; } = [];
        public bool BreakSecondPage { get; set; }
        public bool HideSecondSchedule { get; set; }
        public bool IncludeHomonym { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;
            if (path == "/api/v1/reseller/schedules")
                ScheduleQueries.Add(uri.Query);
            var page = uri.Query.Contains("page=2&", StringComparison.Ordinal) ? 2 : 1;
            var film = new { id = Film };
            var starts = new DateTimeOffset(2026, 11, 16, 23, 10, 0, TimeSpan.Zero);
            object Session(string id, string venue, string filmId = Film) => new { id, attributes = new { show_id = filmId, place_id = venue,
                starts_at = starts.ToOffset(TimeSpan.FromMinutes(210)), purchase_end_at = starts.AddMinutes(-20), status = "available" } };
            object Location(string id, string name, int city, double latitude) => new { id, attributes = new
                { title = name, address = "خیابان نمونه", city_id = city, city_name = name, latitude, longitude = 51,
                    media = new { primary_media = "https://example.test/image.png" } } };
            object Page(int current, int last, int total, params object[] data) => new { data, meta = new { current_page = current, last_page = last, total } };
            object data = path switch
            {
                "/api/v1/shows" when uri.Query.Contains(Art, StringComparison.Ordinal) => Page(1, 1, 0),
                "/api/v1/shows" when IncludeHomonym => Page(1, 1, 2, film, new { id = Homonym }),
                "/api/v1/shows" => Page(1, 1, 1, film),
                "/api/v1/reseller/schedules" when page == 2 && BreakSecondPage => new { data = Array.Empty<object>() },
                "/api/v1/reseller/schedules" when page == 2 => Page(2, 2, 2, Session(SecondSchedule, UnmappedVenue)),
                "/api/v1/reseller/schedules" when IncludeHomonym => Page(1, 1, 2, Session(Schedule, Venue),
                    Session(HomonymSchedule, Venue, Homonym)),
                "/api/v1/reseller/schedules" when HideSecondSchedule => Page(1, 1, 1, Session(Schedule, Venue)),
                "/api/v1/reseller/schedules" => Page(1, 2, 2, Session(Schedule, Venue)),
                "/api/v1/places" => Page(1, 1, 2, Location(Venue, "سینمای اول", 11, 35), Location(UnmappedVenue, "سینمای نامرتبط", 12, 36)),
                "/api/v1/provinces" => new { data = new[] { new { id = 5, name = "تهران", cities = new[]
                    { new { id = 11, name = "تهران" }, new { id = 12, name = "ناشناخته" } } } } },
                _ when path.EndsWith("/" + Film, StringComparison.Ordinal) => new { data = new { id = Film, attributes = new
                    { title = "آزمایش فیلم", summary = "<p>خلاصه</p>", release_date = "2026-01-01",
                        artists = new[] { new { id = "person-001", attributes = new { name = "علي نیک‌نام", role = "بازیگر" } } } } } },
                _ when path.EndsWith("/" + Homonym, StringComparison.Ordinal) => new { data = new { id = Homonym, attributes = new
                    { title = "آزمایش فیلم", release_date = "2024-02-01" } } },
                _ when path.EndsWith("/sessions", StringComparison.Ordinal) => new { data = new[] { new { id = Schedule, attributes = new
                    { starts_at = starts, status = "available", available_count = 1, price = 100, currency_code = "IRR" } } } },
                _ => throw new InvalidOperationException(path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) });
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class References : ISender
    {
        public Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default) =>
            Task.FromResult((T)(object)new GetCitiesResponse([new CityDto(101, "تهران", "Tehran", "tehran", 50, "تهران", 0, true)]));
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Media : IExternalMediaService
    {
        public Task<string?> GetPublicUrlAsync(string url, ExternalMediaOrigin? origin, CancellationToken ct) =>
            Task.FromResult<string?>("/api/media/external/fixture");
        public Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> urls, ExternalMediaOrigin? origin, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(urls.Select(_ => "/api/media/external/fixture").ToArray());
    }

    private sealed class SecondInventory(ICinemaScheduleInventory first, Guid key) : ICinemaScheduleInventory
    {
        public string Key => "second";
        public IQueryable<DiscoveryScreening> Screenings => first.Screenings.Where(x => x.ExternalId == Schedule)
            .Select(x => new DiscoveryScreening
            {
                Id = key, FilmId = x.FilmId, VenueId = x.VenueId, ExternalId = x.ExternalId,
                ExternalFilmId = x.ExternalFilmId, ExternalVenueId = x.ExternalVenueId, StartsAt = x.StartsAt,
                PurchaseStartsAt = x.PurchaseStartsAt, PurchaseEndsAt = x.PurchaseEndsAt, Published = x.Published
            });
        public Task<DiscoveryState?> ReadStateAsync(CancellationToken ct) => first.ReadStateAsync(ct);
    }
}
