using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Hotels.Infrastructure.Workers;
using Refahi.Modules.Media.Application.Contracts.External;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class HotelStaticContentTests
{
    private sealed class Handler(Func<Uri, string> response) : HttpMessageHandler
    {
        public List<Uri> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request.RequestUri!), System.Text.Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Images : IPersistentExternalMedia
    {
        public bool Fail;
        public Task<Guid> EnsureAsync(string url, ExternalMediaOrigin origin, CancellationToken ct) => Fail ? throw new IOException() : Task.FromResult(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        public string GetUrl(Guid id) => "/media/persistent/" + id.ToString("N");
        public bool Exists(Guid id) => true;
    }
    private static HotelStaticContentSynchronizer Synchronizer(Handler handler, Images? images = null, HotelStaticContentOptions? policy = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test") };
        return new(null!, null!, null!, null!, new SnappTripApiClient(http, NullLogger<SnappTripApiClient>.Instance),
            new LamasooApiClient(http, NullLogger<LamasooApiClient>.Instance), images ?? new(), Options.Create(policy ?? new() { SnappTripPublishedReviewStatuses = ["approved"] }), NullLogger<HotelStaticContentSynchronizer>.Instance);
    }
    private static readonly ProviderHotel Hotel = new("1", "10", "هتل", "نشانی", 4, ["https://store.snapptrip.com/a.jpg"]);
    private static string Reply(Uri uri) => uri.AbsolutePath switch
    {
        "/hotels/" => """[{"hotel":{"id":1,"city":{"id":10},"title":"هتل","title_en":"Hotel","accommodation_type":"hotel","reviews":{"ratings":8.5,"reviews":2},"policies":{"check_in_time":"14:00","check_out_time":"12:00","child_age":12,"infant_age":2,"cancellation":"قانون عمومی"}}}]""",
        "/hotels/facilities" => """[{"facilities":[{"title":"استخر","icon":"pool"}]}]""",
        "/hotels/rooms" => """[{"hotel_id":1,"rooms":[{"id":2,"hotel_id":1,"title":"دوتخته","adults":2,"children":1,"extra_bed":1,"description":"اتاق","board_type":"BB","facilities_tags":[]}]}]""",
        "/hotels/galleries" => """[{"hotel_id":1,"gallery":[{"url":"https://store.snapptrip.com/a.jpg","title":"نما","description":"ورودی"}]}]""",
        "/hotels/reviews" => """[{"hotel_id":1,"reviews":[{"id":1,"hotel_id":1,"user_id":999,"fullname":"نام","comment":"<p>خوب</p><script>bad()</script>","status":"approved","rate_overall":8,"registered_date":100,"comment_risk_level":0.2},{"id":2,"hotel_id":1,"comment":"private","status":"unknown","rate_overall":9}]}]""",
        _ => throw new InvalidOperationException("Unexpected supplier call " + uri.AbsolutePath)
    };
    [Fact] public async Task FullStaticSynchronizationNeverRequestsAvailabilityAndKeepsPresentation()
    {
        var handler = new Handler(Reply);
        var content = await Synchronizer(handler).FetchContent("SnappTrip", Hotel, null, default);
        Assert.True(content.IsReady); Assert.Equal("14:00", content.Policies.CheckInTime);
        Assert.Equal("Hotel", content.EnglishName); Assert.Equal(8.5, content.Rating);
        Assert.Equal("pool", Assert.Single(content.Facilities).Icon);
        Assert.Equal(1, Assert.Single(content.Rooms).ExtraBeds); Assert.Equal("نما", Assert.Single(content.Gallery).Title);
        Assert.Equal("خوب", Assert.Single(content.Reviews).Text);
        var json = JsonSerializer.Serialize(content);
        Assert.DoesNotContain("user_id", json); Assert.DoesNotContain("risk", json); Assert.DoesNotContain("private", json); Assert.DoesNotContain("bad()", json);
        Assert.Equal(5, handler.Calls.Count); Assert.DoesNotContain(handler.Calls, x => x.AbsolutePath.Contains("availability"));
    }
    [Fact] public async Task FailedReviewsAndImagesPreserveLastSuccessfulSections()
    {
        var previous = await Synchronizer(new Handler(Reply)).FetchContent("SnappTrip", Hotel, null, default);
        previous = previous with { Sections = previous.Sections.ToDictionary(x => x.Key, x => x.Value with { NextAttemptAtUtc = DateTimeOffset.UtcNow.AddDays(-1) }) };
        var handler = new Handler(uri => uri.AbsolutePath.Contains("reviews") ? "[]" : Reply(uri));
        var content = await Synchronizer(handler, new() { Fail = true }).FetchContent("SnappTrip", Hotel, previous, default);
        Assert.Equal(previous.Reviews, content.Reviews); Assert.Equal(previous.Gallery, content.Gallery);
        Assert.Equal("failed", content.Sections["reviews"].Status); Assert.Equal("failed", content.Sections["gallery"].Status);
        Assert.Equal(previous.Sections["reviews"].LastSuccessAtUtc, content.Sections["reviews"].LastSuccessAtUtc);
        Assert.Equal("ready", content.Sections["rooms"].Status); Assert.False(content.IsReady);
    }
    [Fact] public async Task UnverifiedReviewStatusBlocksReadinessRatherThanPublishingUnreviewedText()
    {
        var handler = new Handler(Reply);
        var content = await Synchronizer(handler, policy: new()).FetchContent("SnappTrip", Hotel, null, default);
        Assert.Empty(content.Reviews); Assert.Equal("failed", content.Sections["reviews"].Status); Assert.False(content.IsReady);
        Assert.DoesNotContain(handler.Calls, x => x.AbsolutePath.Contains("reviews"));
    }
    [Fact] public async Task FreshSectionsDoNotFetchAgain()
    {
        var previous = await Synchronizer(new Handler(Reply)).FetchContent("SnappTrip", Hotel, null, default);
        var handler = new Handler(_ => throw new InvalidOperationException("Network blocked"));
        var result = await Synchronizer(handler).FetchContent("SnappTrip", Hotel, previous, default);
        Assert.Empty(handler.Calls); Assert.True(result.IsReady);
    }
    [Fact] public async Task LamasooDoesNotTreatInventoryRatesOrCapacityRemainingAsStaticContent()
    {
        var handler = new Handler(_ => """{"hotels":[{"id":"1","city":"10","room_types":[{"id":"r","title":"Room","capacity":2,"extra_capacity":1,"rooms_remaining":999}],"room_rates":[{"prices":[{"display_price":999}]}]}]}""");
        var result = await Synchronizer(handler).FetchContent("Lamasoo", Hotel, null, default);
        Assert.True(result.IsReady); Assert.Equal("unsupported", result.Sections["reviews"].Status);
        var json = JsonSerializer.Serialize(result.Rooms); Assert.DoesNotContain("999", json); Assert.DoesNotContain("price", json, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Calls);
    }
    [Fact] public void RolloutDefaultsAreOffAndRequireExplicitCityScopes()
    {
        var options = new HotelStaticContentOptions(); Assert.False(options.WorkerEnabled); Assert.False(options.Includes("SnappTrip", 1));
        options.ReadEnabled = true; Assert.False(options.Includes("SnappTrip", 1));
        options.ReadScopes = ["SnappTrip:1"]; Assert.True(options.Includes("SnappTrip", 1)); Assert.False(options.Includes("Lamasoo", 1));
    }
    [Fact] public void ModelHasOnlyAdditiveNullableStorageAndArrayFiltersTranslate()
    {
        using var db = new HotelsDbContext(new DbContextOptionsBuilder<HotelsDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var entity = db.Model.FindEntityType(typeof(SnappTripDiscoveryHotel))!;
        Assert.True(entity.FindProperty(nameof(HotelDiscovery.ContentJson))!.IsNullable);
        Assert.Contains("FacilityKeys", db.Set<SnappTripDiscoveryHotel>().Where(x => x.FacilityKeys != null && x.FacilityKeys.Contains("استخر")).ToQueryString());
    }
    [Fact] public async Task LocalAvailabilityAndCapacityValidationNeverFetchStaticSupplierEndpoints()
    {
        var content = await Synchronizer(new Handler(Reply)).FetchContent("SnappTrip", Hotel, null, default);
        var key = Guid.NewGuid();
        var profile = new HotelDiscoveryProfile(key, 1, "شهر", "استان", "SnappTrip", "نام محلی", "شرح", "نشانی", 4,
            ["https://media.test/media/persistent/image"], ["استخر"], null, DateTimeOffset.UtcNow, false, Content: content, IsLocalContent: true, NativeId: "1");
        var local = new Local(profile);
        var handler = new Handler(uri => uri.AbsolutePath == "/availability/cities"
            ? """{"items":[{"city_id":10,"hotel":{"id":1,"title":"live","stars":4},"room":{"id":2,"title":"room","price":100}}]}"""
            : throw new InvalidOperationException("Static supplier network prohibited"));
        var api = new SnappTripApiClient(new HttpClient(handler) { BaseAddress = new("https://fixture.test") }, NullLogger<SnappTripApiClient>.Instance);
        var provider = new SnappTripHotelProvider(api, NullLogger<SnappTripHotelProvider>.Instance, local);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var adapter = new SnappTripCatalogueProvider(provider, api, cache, null!, local);
        var travel = new HotelTravel(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), [new(2, [])]);
        var offer = Assert.Single(await adapter.SearchAsync("10", travel, default));
        Assert.Equal(profile.Images, offer.Hotel.Images);
        var validated = await adapter.ValidateOfferAsync(offer, travel, default); Assert.Equal(2, validated.AdultCapacity);
        Assert.Equal("/availability/cities", Assert.Single(handler.Calls).AbsolutePath);
    }
    private sealed class Local(HotelDiscoveryProfile profile) : IHotelLocalContentReader
    {
        public Task<bool> UsesLocalAsync(Guid key, CancellationToken ct) => Task.FromResult(true);
        public Task<Guid?> ResolveLegacyKeyAsync(long id, CancellationToken ct) => Task.FromResult<Guid?>(profile.HotelKey);
        public Task<HotelDiscoveryProfile?> ProfileAsync(Guid key, CancellationToken ct) => Task.FromResult<HotelDiscoveryProfile?>(profile);
        public Task<HotelDiscoveryProfile?> LegacyProfileAsync(long id, CancellationToken ct) => Task.FromResult<HotelDiscoveryProfile?>(profile);
        public Task<IReadOnlyList<HotelDiscoveryProfile>?> ProviderHotelsAsync(string provider, string city, CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelDiscoveryProfile>?>([profile]);
        public Task<HotelLocalSearchPage> SearchAsync(HotelLocalSearch query, CancellationToken ct) => throw new NotSupportedException();
        public Task<HotelReviewsPage?> ReviewsAsync(Guid key, int page, int size, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<HotelContentReadiness>> ReadinessAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
