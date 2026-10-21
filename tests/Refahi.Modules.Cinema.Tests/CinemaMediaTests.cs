using System.Text.Json;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Application.Features.Catalog;
using Refahi.Modules.Media.Application.Contracts.External;
using Xunit;

namespace Refahi.Modules.Cinema.Tests;

internal sealed class RecordingCinemaMedia : IExternalMediaService
{
    public int Calls;
    public Exception? Failure;
    public ExternalMediaOrigin? Origin;
    public string[] Sources = [];
    private readonly Dictionary<string, string> urls = new(StringComparer.Ordinal);
    public Task<string?> GetPublicUrlAsync(string sourceUrl, ExternalMediaOrigin? origin = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Images must be registered in a batch.");
    public Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> sourceUrls, ExternalMediaOrigin? origin = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); Calls++; Origin = origin; Sources = sourceUrls.ToArray();
        if (Failure is not null) throw Failure;
        return Task.FromResult<IReadOnlyList<string>>(Sources.Select(s =>
        {
            if (!urls.TryGetValue(s, out var url)) urls[s] = url = "https://media.refahi.test/media/external/" + Guid.NewGuid().ToString("N");
            return url;
        }).ToArray());
    }
}

public sealed class CinemaMediaTests
{
    internal const string Image = "https://console.iticket.ir/api/image/image-id/full";
    internal static CinemaShow Show => new("show", "فیلم", "cinema", Image, Image,
        "<img src='" + Image + "'>", "<img src='" + Image + "'>", 90, null, ["هنرمند"],
        ArtistDetails: [new("artist", "هنرمند", Image + "?portrait=1")], SummaryHtml: "<p>خلاصه</p>", DescriptionHtml: "<p>توضیح</p>");
    internal static CinemaSeatMap Map => new("schedule", Show, new("place", "سینما", null, null, Image + "?place=1"),
        "سالن", DateTimeOffset.UtcNow.AddDays(1), null, []);

    [Fact]
    public async Task LandingRegistersOneBatchAndHidesEveryImageSourceWithoutMutatingProviderData()
    {
        var registry = new RecordingCinemaMedia(); var publisher = new CinemaMediaPublisher(registry);
        var source = new CinemaLanding([new("بنر", Image, "/cinema/shows/show")], [Show, Show], [Show], [Show], []);
        var result = await publisher.PublishAsync(source, "iticket", default);
        Assert.Equal(1, registry.Calls); Assert.Equal(2, registry.Sources.Length);
        Assert.Equal(new ExternalMediaOrigin("Cinema", "iticket"), registry.Origin);
        Assert.DoesNotContain("console.iticket.ir", JsonSerializer.Serialize(result));
        Assert.Equal(result.Banners[0].ImageUrl, result.Cinema[0].Poster);
        Assert.Equal("/cinema/shows/show", result.Banners[0].Url);
        Assert.Equal("<p>توضیح</p>", result.Cinema[0].Description);
        Assert.Contains("console.iticket.ir", JsonSerializer.Serialize(source));
    }

    [Fact]
    public async Task DetailCatalogPlacesAndSeatMapUseRefahiUrls()
    {
        var registry = new RecordingCinemaMedia(); var publisher = new CinemaMediaPublisher(registry);
        object[] results = [await publisher.PublishAsync(Show, "iticket", default),
            await publisher.PublishAsync(new CinemaCatalog([Show], 1, 2), "iticket", default),
            await publisher.PublishAsync(new CinemaDisplayDay[] { new("2026-10-25", "روز", "روز", [Map.Place, Map.Place]) }, "iticket", default),
            await publisher.PublishAsync(Map, "iticket", default)];
        Assert.All(results, r => Assert.DoesNotContain("console.iticket.ir", JsonSerializer.Serialize(r)));
        Assert.Equal(4, registry.Calls);
    }

    [Fact]
    public async Task InvalidImageDoesNotShiftTheMappingOfOtherImages()
    {
        var registry = new RecordingCinemaMedia(); var publisher = new CinemaMediaPublisher(registry);
        var result = await publisher.PublishAsync(Show with { Poster = "http://console.iticket.ir/insecure" }, "iticket", default);
        Assert.Null(result.Poster); Assert.NotNull(result.Cover); Assert.NotNull(result.ArtistDetails![0].Portrait);
        Assert.All(registry.Sources, url => Assert.StartsWith("https://", url));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailingMediaServiceRemovesSourcesAndKeepsTheContentUsable(bool failing)
    {
        var publisher = new CinemaMediaPublisher(failing ? new RecordingCinemaMedia { Failure = new IOException() } : null);
        var result = await publisher.PublishAsync(Map, "iticket", default);
        Assert.Equal(Map.Show.Title, result.Show.Title); Assert.Equal(Map.Place.Title, result.Place.Title);
        Assert.Null(result.Show.Poster); Assert.Null(result.Place.Cover);
        Assert.DoesNotContain("console.iticket.ir", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task CancellationIsNotSwallowedAsAnImageFallback()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var publisher = new CinemaMediaPublisher(new RecordingCinemaMedia());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(Show, "iticket", canceled.Token));
    }
}
