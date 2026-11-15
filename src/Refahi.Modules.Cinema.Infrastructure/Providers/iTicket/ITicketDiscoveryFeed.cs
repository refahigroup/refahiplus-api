using System.Globalization;
using System.Text.Json;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Catalogue.Application.Contracts;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

internal sealed record ITicketCredit(CinemaArtist Artist, string[] Aliases, string? Role);
internal sealed record ITicketFilm(CinemaShow Show, string[] Aliases, int? ReleaseYear, ITicketCredit[]? Credits);
internal sealed record ITicketVenue(CinemaPlace Place, string[] Aliases, int? CityId, CataloguePoint? Point);
internal sealed record ITicketCity(int Id, int ProvinceId, string Name, string Province);
internal sealed record ITicketSession(string Id, string FilmId, string PlaceId, DateTimeOffset StartsAt,
    DateTimeOffset? PurchaseStartsAt, DateTimeOffset? PurchaseEndsAt, string Status, bool Available);
internal sealed record ITicketDiscoveryFeed(ITicketFilm[] Films, ITicketVenue[] Venues,
    ITicketCity[] Cities, ITicketSession[] Sessions);

public sealed partial class ITicketCinemaProvider
{
    private static readonly TimeZoneInfo DiscoveryTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    private const string InvalidDiscoveryData = "داده همگام‌سازی سینما کامل یا معتبر نیست";

    internal async Task<ITicketDiscoveryFeed> ReadDiscoveryAsync(DateTimeOffset now, int horizonDays, CancellationToken ct)
    {
        var categories = Settings.CinemaCategories.Concat(Settings.ArtCategories).Distinct().ToArray();
        Check(Settings.CinemaCategories.Length > 0 && Settings.ArtCategories.Length > 0
            && categories.All(CinemaScheduleIdentity.IsLegacyId)
            && Settings.DiscoveryAvailableStatuses.Any(s => !string.IsNullOrWhiteSpace(s)));

        var films = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (kind, scope) in new[] { ("art", Settings.ArtCategories), ("cinema", Settings.CinemaCategories) })
        {
            if (scope.Length == 0) continue;
            var filter = CategoryFilter(scope);
            foreach (var row in await ReadPagesAsync(page =>
                $"shows?page={page}&per_page=24&sort=-display_priority&only_with_active_sessions=true{filter}", ct))
                films.TryAdd(S(row, "id"), kind);
        }

        var horizon = now.AddDays(horizonDays);
        var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, DiscoveryTimeZone).DateTime);
        var localEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(horizon, DiscoveryTimeZone).DateTime);
        var categoryFilter = CategoryFilter(categories);
        var scheduleRows = new List<JsonElement>();
        for (var date = localStart; date <= localEnd; date = date.AddDays(1))
        {
            var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            scheduleRows.AddRange(await ReadPagesAsync(page =>
                $"reseller/schedules?page={page}&per_page=500{categoryFilter}&starts_from={day}&starts_to={day}", ct));
        }
        var sessions = scheduleRows.DistinctBy(row => S(row, "id"), StringComparer.Ordinal).Select(MapDiscoverySession)
            .Where(row => films.ContainsKey(row.FilmId) && row.StartsAt > now && row.StartsAt <= horizon)
            .ToArray();

        var placeIds = sessions.Select(s => s.PlaceId).ToHashSet(StringComparer.Ordinal);
        var placeRows = placeIds.Count == 0 ? [] : await ReadPagesAsync(page => $"places?page={page}&per_page=24", ct);
        var places = placeRows.Where(row => placeIds.Contains(S(row, "id"))).ToDictionary(row => S(row, "id"), StringComparer.Ordinal);
        var provinceRows = Resource(await Read("provinces", ct));
        Check(provinceRows.ValueKind == JsonValueKind.Array);
        var cities = Items(provinceRows).SelectMany(province =>
        {
            Check(P(province, "cities").ValueKind == JsonValueKind.Array);
            return Items(P(province, "cities")).Select(city => new ITicketCity((int)N(city, "id"),
                (int)N(province, "id"), S(city, "name"), S(province, "name")));
        }).ToArray();
        Check(cities.All(city => city.Id > 0 && city.ProvinceId > 0
            && !string.IsNullOrWhiteSpace(city.Name) && !string.IsNullOrWhiteSpace(city.Province))
            && cities.Select(city => city.Id).Distinct().Count() == cities.Length);

        var filmDetails = new ITicketFilm[films.Count];
        var venueDetails = new ITicketVenue[placeIds.Count];
        // Use the same concurrency budget for film and venue detail requests.
        var work = films.Select((row, index) => (Id: row.Key, Kind: (string?)row.Value, Index: index))
            .Concat(placeIds.Select((id, index) => (Id: id, Kind: (string?)null, Index: index)));
        await Parallel.ForEachAsync(work, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (item, token) =>
            {
                if (item.Kind is not null)
                {
                    var row = Resource(await Read("shows/" + E(item.Id), token));
                    Check(S(row, "id") == item.Id);
                    var attrs = A(row);
                    var film = MapShow(row, item.Kind);
                    var artists = P(attrs, "artists");
                    Check(P(attrs, "title").ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(film.Title) && artists.ValueKind
                        is JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined);
                    var credits = artists.ValueKind == JsonValueKind.Array
                        ? Items(artists).Select(artist => new ITicketCredit(MapArtist(artist),
                            Aliases(A(Resource(artist))), CleanString(P(A(Resource(artist)), "role")) ?? CleanString(P(artist, "role"))))
                            .Where(credit => !string.IsNullOrWhiteSpace(credit.Artist.Name)).ToArray()
                        : null;
                    var year = Date(attrs, "release_date")?.Year;
                    filmDetails[item.Index] = new(film, Aliases(attrs), year, credits);
                }
                else
                {
                    var venue = places.TryGetValue(item.Id, out var row) ? MapDiscoveryVenue(row) : null;
                    if (venue is null || venue.CityId is null || venue.Point is null || string.IsNullOrWhiteSpace(venue.Place.Address)
                        || string.IsNullOrWhiteSpace(venue.Place.Cover))
                    {
                        var detail = Resource(await Read("places/" + E(item.Id), token));
                        Check(S(detail, "id") == item.Id);
                        var mapped = MapDiscoveryVenue(detail);
                        venue = mapped with
                        {
                            CityId = mapped.CityId ?? venue?.CityId,
                            Point = mapped.Point ?? venue?.Point,
                            Place = mapped.Place with
                            {
                                Address = BlankToNull(mapped.Place.Address) ?? venue?.Place.Address,
                                Cover = mapped.Place.Cover ?? venue?.Place.Cover
                            }
                        };
                    }
                    Check(!string.IsNullOrWhiteSpace(venue.Place.Title));
                    venueDetails[item.Index] = venue;
                }
            });
        return new(filmDetails, venueDetails, cities, sessions);
    }

    private async Task<List<JsonElement>> ReadPagesAsync(Func<int, string> path, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rows = await TryReadPagesAsync(path, ct);
            if (rows is not null) return rows;
        }
        throw new CinemaException(InvalidDiscoveryData, 502);
    }

    private async Task<List<JsonElement>?> TryReadPagesAsync(Func<int, string> path, CancellationToken ct)
    {
        var rows = new List<JsonElement>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int? lastPage = null, total = null;
        for (var page = 1; lastPage is null || page <= lastPage; page++)
        {
            var root = await Read(path(page), ct);
            var data = P(root, "data");
            var meta = P(root, "meta");
            var reportedLast = N(meta, "last_page");
            var reportedTotal = N(meta, "total", -1);
            Check(data.ValueKind == JsonValueKind.Array && N(meta, "current_page") == page
                && reportedLast is >= 1 and <= 10000 && reportedTotal is >= 0 and <= 240000);
            lastPage ??= (int)reportedLast;
            total ??= (int)reportedTotal;
            if (lastPage != reportedLast || total != reportedTotal) return null;
            foreach (var row in Items(data))
            {
                var id = S(row, "id");
                Check(CinemaScheduleIdentity.IsLegacyId(id));
                if (!ids.Add(id)) return null;
                rows.Add(row);
            }
        }
        return rows.Count == total ? rows : null;
    }

    private ITicketSession MapDiscoverySession(JsonElement row)
    {
        var attrs = A(row);
        var id = S(row, "id");
        var film = S(attrs, "show_id");
        var place = S(attrs, "place_id");
        var status = S(attrs, "status");
        var starts = Date(attrs, "starts_at");
        var opens = Date(attrs, "purchase_start_at");
        var ends = Date(attrs, "purchase_end_at");
        Check(CinemaScheduleIdentity.IsLegacyId(film) && CinemaScheduleIdentity.IsLegacyId(place)
            && starts.HasValue && !string.IsNullOrWhiteSpace(status)
            && (opens.HasValue || string.IsNullOrWhiteSpace(S(attrs, "purchase_start_at")))
            && (ends.HasValue || string.IsNullOrWhiteSpace(S(attrs, "purchase_end_at"))));
        return new(id, film, place, starts!.Value.ToUniversalTime(), opens?.ToUniversalTime(), ends?.ToUniversalTime(),
            status, Settings.DiscoveryAvailableStatuses.Contains(status, StringComparer.OrdinalIgnoreCase));
    }

    private static ITicketVenue MapDiscoveryVenue(JsonElement row)
    {
        var attrs = A(row);
        var place = MapPlace(row);
        var city = N(attrs, "city_id", N(P(attrs, "city"), "id"));
        var address = CleanString(P(attrs, "address")) ?? CleanString(P(P(attrs, "contact"), "address"));
        CataloguePoint? point = double.TryParse(S(attrs, "latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(S(attrs, "longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
            ? new(lat, lng) : null;
        return new(place with { Address = address }, Aliases(attrs), city is > 0 and <= int.MaxValue ? (int)city : null,
            point?.IsValid == true && (point.Latitude != 0 || point.Longitude != 0) ? point : null);
    }

    private static string[] Aliases(JsonElement attrs) => Items(P(attrs, "aliases")).Where(x => x.ValueKind == JsonValueKind.String)
        .Select(Text).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string? CleanString(JsonElement value) => value.ValueKind == JsonValueKind.String ? BlankToNull(value.GetString()) : null;
    private static string CategoryFilter(IEnumerable<string> categories) => string.Concat(categories.Select(x => "&category[]=" + E(x)));

    private static void Check(bool valid)
    {
        if (!valid) throw new CinemaException(InvalidDiscoveryData, 502);
    }
}
