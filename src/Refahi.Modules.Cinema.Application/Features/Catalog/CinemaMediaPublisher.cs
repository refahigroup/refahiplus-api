using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Media.Application.Contracts.External;

namespace Refahi.Modules.Cinema.Application.Features.Catalog;

public sealed class CinemaMediaPublisher(IExternalMediaService? media = null, ILogger<CinemaMediaPublisher>? logger = null)
{
    public async Task<CinemaLanding> PublishAsync(CinemaLanding value, string provider, CancellationToken ct)
    {
        var shows = value.Cinema.Concat(value.Theater).Concat(value.Art).ToArray();
        var map = await MapAsync(shows.SelectMany(Images).Concat(value.Banners.Select(b => b.ImageUrl)), provider, ct);
        return value with
        {
            Banners = value.Banners.Select(b => b with { ImageUrl = Url(map, b.ImageUrl) ?? "" }).Where(b => b.ImageUrl.Length > 0).ToArray(),
            Cinema = value.Cinema.Select(s => Show(s, map)).ToArray(),
            Theater = value.Theater.Select(s => Show(s, map)).ToArray(),
            Art = value.Art.Select(s => Show(s, map)).ToArray()
        };
    }

    public async Task<CinemaCatalog> PublishAsync(CinemaCatalog value, string provider, CancellationToken ct)
    {
        var map = await MapAsync(value.Items.SelectMany(Images), provider, ct);
        return value with { Items = value.Items.Select(s => Show(s, map)).ToArray() };
    }

    public async Task<CinemaShow> PublishAsync(CinemaShow value, string provider, CancellationToken ct)
        => Show(value, await MapAsync(Images(value), provider, ct));

    public async Task<IReadOnlyList<CinemaDisplayDay>> PublishAsync(IReadOnlyList<CinemaDisplayDay> value, string provider, CancellationToken ct)
    {
        var map = await MapAsync(value.SelectMany(d => d.Places).Select(p => p.Cover), provider, ct);
        return value.Select(d => d with { Places = d.Places.Select(p => Place(p, map)).ToArray() }).ToArray();
    }

    public async Task<CinemaSeatMap> PublishAsync(CinemaSeatMap value, string provider, CancellationToken ct)
    {
        var map = await MapAsync(Images(value.Show).Append(value.Place.Cover), provider, ct);
        return value with { Show = Show(value.Show, map), Place = Place(value.Place, map) };
    }

    private static IEnumerable<string?> Images(CinemaShow show)
        => new[] { show.Poster, show.Cover }.Concat((show.ArtistDetails ?? []).Select(a => a.Portrait));

    private static CinemaShow Show(CinemaShow show, IReadOnlyDictionary<string, string> map) => show with
    {
        Poster = Url(map, show.Poster), Cover = Url(map, show.Cover),
        ArtistDetails = show.ArtistDetails?.Select(a => a with { Portrait = Url(map, a.Portrait) }).ToArray(),
        // Raw supplier HTML can contain image sources even though the rendered HTML has already been sanitized.
        Summary = show.SummaryHtml ?? show.SummaryText, Description = show.DescriptionHtml
    };
    private static CinemaPlace Place(CinemaPlace place, IReadOnlyDictionary<string, string> map)
        => place with { Cover = Url(map, place.Cover) };
    private static string? Url(IReadOnlyDictionary<string, string> map, string? source)
        => Normalize(source) is { } normalized ? map.GetValueOrDefault(normalized) : null;
    private static string? Normalize(string? source)
        => source?.Length <= 8192 && Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;

    private async Task<IReadOnlyDictionary<string, string>> MapAsync(IEnumerable<string?> images, string provider, CancellationToken ct)
    {
        var sources = images.Select(Normalize).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (media is null || sources.Length == 0) return new Dictionary<string, string>();
        try
        {
            var urls = await media.GetPublicUrlsAsync(sources, new("Cinema", provider), ct);
            // Registration preserves order for valid normalized URLs; fail closed on an incomplete result.
            if (urls.Count != sources.Length) return new Dictionary<string, string>();
            return sources.Zip(urls).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (logger ?? NullLogger<CinemaMediaPublisher>.Instance).LogWarning("Cinema media registration unavailable Provider={Provider} ErrorType={ErrorType}", provider, ex.GetType().Name);
            return new Dictionary<string, string>();
        }
    }
}
