using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

internal static class HotelLocalContentPostgresChecks
{
    internal static async Task Run(HotelsDbContext db, SnappTripCityMapping mapping)
    {
        var scope = Options.Create(new HotelStaticContentOptions { ReadEnabled = true, ReadScopes = [$"SnappTrip:{mapping.CityId}"] });
        var media = new LocalImages(); var geography = new Geography(mapping.CityId);
        var local = new HotelLocalContentReader(db, geography, media, scope);
        Guid first = default;
        for (var i = 0; i < 3; i++)
        {
            var listing = HotelListing.Create(mapping.CityId, "SnappTrip", "local-content-" + i, mapping.ProviderCityId);
            var hotel = SnappTripDiscoveryHotel.Create(listing.HotelKey, listing.NativeId, mapping.ProviderCityId);
            if (i == 0) first = listing.HotelKey;
            hotel.Publish(mapping.CityId, "محلی " + i, "شرح", "نشانی", i + 3, "[]", "[]", null, "محلی استخر", "local " + i, DateTimeOffset.UtcNow.AddDays(-30), 35 + i, 51);
            var sections = new[] { "profile", "rooms", "facilities", "gallery", "reviews" }.ToDictionary(x => x, _ => new HotelContentSection("ready", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-29)));
            var content = new HotelStaticContent("Local", "hotel", "هتل", new("14:00", "12:00"), [new("استخر")],
                [new("r", "اتاق محلی", "شرح اتاق", 2, 1, 1, "BB", [])],
                [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "نما", "", true, "https://private-source.test/a")],
                [new("1", "نام", "نظر قدیمی", 7, 100, 100, false, false, new Dictionary<string, double>()), new("2", "نام", "نظر تازه", 9, 200, 200, true, true, new Dictionary<string, double>())], 7 + i, 2, sections);
            hotel.UpdateContent(JsonSerializer.Serialize(content, HotelLocalContentReader.Json), "hotel", 7 + i, ["استخر"], true);
            db.AddRange(listing, hotel);
        }
        await db.SaveChangesAsync();
        var page = await local.SearchAsync(new(Query: "محلی", Stars: [3, 4, 5], Facilities: ["استخر"], Accommodations: ["hotel"], MinRating: 8, Sort: "rating", PageSize: 1), default);
        Assert.Equal(2, page.TotalCount); Assert.Equal(5, Assert.Single(page.Items).Stars);
        Assert.All(page.Items, x => Assert.True(x.IsStale));
        var next = await local.SearchAsync(new(Query: "محلی", Sort: "title", Page: 2, PageSize: 1), default);
        Assert.Equal("محلی 1", Assert.Single(next.Items).Title);
        var nearby = await local.SearchAsync(new(Query: "محلی", Latitude: 35, Longitude: 51, Sort: "distance"), default);
        Assert.Equal(first, nearby.Items[0].HotelKey);
        var profile = await local.ProfileAsync(first, default); Assert.NotNull(profile); Assert.True(profile.IsLocalContent); Assert.True(profile.IsStale);
        Assert.Contains("/media/persistent/", Assert.Single(profile.Images)); Assert.Empty(profile.Content!.Reviews);
        Assert.Null(Assert.Single(profile.Content.Gallery).SourceUrl);
        var reviews = await local.ReviewsAsync(first, 1, 1, default); Assert.Equal(2, reviews!.TotalCount); Assert.Equal("2", Assert.Single(reviews.Items).Id);
        Assert.False((await local.ReviewsAsync(first, 2, 1, default))!.Items.Single().Recommended);
        var discovery = new HotelDiscoveryReader(db, geography, new DisabledRegistry(), Options.Create(new HotelDiscoveryOptions()), staticOptions: scope, local: local);
        Assert.Equal(3, (await discovery.SearchAsync("محلی", mapping.CityId, 20, default)).Items.Count);
        Assert.NotNull(await discovery.ProfileAsync(first, default));
        Assert.True((await local.ReadinessAsync(default)).Single(x => x.Provider == "SnappTrip" && x.ReferenceCityId == mapping.CityId).Ready == false); // Other historical fixtures intentionally have no content.
        var row = await db.Set<SnappTripDiscoveryHotel>().SingleAsync(x => x.HotelKey == first); row.Retire(); await db.SaveChangesAsync();
        Assert.Null(await local.ProfileAsync(first, default)); Assert.Null(await local.ReviewsAsync(first, 1, 20, default));
        Assert.Equal(2, (await local.SearchAsync(new(Query: "محلی"), default)).TotalCount);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => local.SearchAsync(new(Page: 0), default));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(0, media.Downloads);
    }
    private sealed class LocalImages : IPersistentExternalMedia
    {
        public int Downloads;
        public Task<Guid> EnsureAsync(string url, ExternalMediaOrigin origin, CancellationToken ct) { Downloads++; throw new InvalidOperationException("Network blocked"); }
        public string GetUrl(Guid id) => "/media/persistent/" + id.ToString("N");
        public bool Exists(Guid id) => true;
    }
    private sealed class DisabledRegistry : IHotelCatalogueProviderRegistry
    {
        public IReadOnlyList<IHotelCatalogueProvider> Active => [];
        public IHotelCatalogueProvider Get(string name) => throw new InvalidOperationException("Supplier access prohibited");
    }
    private sealed class Geography(int city) : ISender
    {
        public Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default) => Task.FromResult((T)(object)(request switch
        {
            GetCitiesQuery => new GetCitiesResponse([new(city, "شهر", "City", "city", 1, "استان", 0, true)]),
            GetProvincesQuery => new GetProvincesResponse([new(1, "استان", "Province", "province", 0, true)]),
            _ => throw new InvalidOperationException("Unexpected query")
        }));
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
