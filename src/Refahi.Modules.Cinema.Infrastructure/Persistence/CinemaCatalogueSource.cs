using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Cinema.Application.Contracts;

namespace Refahi.Modules.Cinema.Infrastructure.Persistence;

internal sealed class CinemaCatalogueSource(CinemaDbContext db, IEnumerable<ICinemaScheduleInventory> inventories,
    ICinemaProviderFactory providers, TimeProvider clock, IOptions<CinemaDiscoveryOptions> options,
    ILogger<CinemaCatalogueSource>? logger = null)
    : ICatalogueSource, ICinemaDiscoveryReader
{
    private const string Source = "cinema";
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    public async Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query, int take, CancellationToken ct)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var (screenings, partial) = await ReadAsync(null, null, ct);
        var venues = await ReadVenuesAsync(screenings, query.CityId, query.ProvinceId, ct);
        var term = CatalogueText.Normalize(query.Q);
        var filmsByVenue = screenings.Where(x => venues.ContainsKey(x.Row.VenueId)).ToLookup(x => x.Row.FilmId);
        var ids = filmsByVenue.Select(x => x.Key).ToArray();
        var films = await db.Set<CinemaFilm>().AsNoTracking().AsSplitQuery().Include(x => x.Credits).ThenInclude(x => x.Person)
            .Where(x => ids.Contains(x.Id) && (x.Kind == "cinema" || x.Kind == "art")).ToListAsync(ct);
        var results = new List<CatalogueItem>(films.Count);
        foreach (var film in films)
        {
            var category = Category(film.Kind);
            var filmMatches = Matches(term, film.Title, category.Title)
                || Matches(term, film.Aliases.Concat(film.Genres))
                || film.Credits.Any(x => Matches(term, x.Person.Name) || Matches(term, x.Person.Aliases));
            var locations = filmsByVenue[film.Id].Where(x =>
            {
                var venue = venues[x.Row.VenueId];
                return filmMatches || Matches(term, venue.Name, venue.Address) || Matches(term, venue.Aliases);
            }).ToArray();
            if (locations.Length == 0) continue;
            var id = film.Id.ToString("D");
            var observed = locations.Min(x => x.ObservedAt);
            var item = CatalogueOrdering.Prepare(new CatalogueItem($"{Source}:{id}", Source, film.Kind, id,
                film.Title, film.SummaryText, film.Poster ?? film.Cover, null, null, null, film.Genres,
                $"/cinema/shows/{id}", observed, locations.All(x => x.IsStale), Category: category,
                Locations: locations.Select(x => venues[x.Row.VenueId]).DistinctBy(x => x.Id).Select(Location).ToArray()), query);
            if (!filmMatches && item.DisplayLocation is { } matched)
                item = item with { DetailPath = item.DetailPath + "?place=" + matched.Id };
            results.Add(item);
        }
        await snapshot.CommitAsync(ct);
        return new(results.OrderBy(x => x, CatalogueOrdering.Comparer).Take(take).ToArray(), partial ? null : results.Count, partial);
    }

    public async Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct) => (await ReadCitiesAsync(ct)).Cities;

    public async Task<CatalogueSourceCities> ReadCitiesAsync(CancellationToken ct)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var (screenings, partial) = await ReadAsync(null, null, ct);
        var venues = await ReadVenuesAsync(screenings, null, null, ct);
        var cities = venues.Values.Where(x => x.CityId.HasValue && x.ProvinceId.HasValue).DistinctBy(x => x.CityId)
            .Select(x => new CatalogueCity(x.CityId!.Value, x.CityName!, x.ProvinceName!, x.ProvinceId!.Value)).ToArray();
        await snapshot.CommitAsync(ct);
        return new(cities, partial);
    }

    async Task<IReadOnlyList<CinemaCity>> ICinemaDiscoveryReader.CitiesAsync(CancellationToken ct) =>
        (await CitiesAsync(ct)).OrderBy(x => CatalogueText.Normalize(x.Name), CatalogueText.Comparer)
            .Select(x => new CinemaCity(x.Id, x.Name, x.ProvinceName)).ToArray();

    public async Task<CinemaShow> ShowAsync(Guid show, CancellationToken ct)
    {
        var film = await db.Set<CinemaFilm>().AsNoTracking().Include(x => x.Credits).ThenInclude(x => x.Person)
            .SingleOrDefaultAsync(x => x.Id == show, ct) ?? throw new CinemaException("نمایش موردنظر یافت نشد", 404);
        var credits = film.Credits.OrderBy(x => x.SortOrder).Select(x =>
            new CinemaArtist(x.PersonId.ToString("D"), x.Person.Name, x.Person.Portrait)).ToArray();
        return new(film.Id.ToString("D"), film.Title, film.Kind, film.Poster, film.Cover, film.SummaryHtml ?? film.SummaryText,
            film.DescriptionHtml, film.DurationMinutes, film.AgeGroup, credits.Select(x => x.Name).ToArray(),
            film.Genres.Select(x => new CinemaGenre(x, x)).ToArray(), credits, film.SummaryHtml, film.DescriptionHtml, film.SummaryText);
    }

    public async Task<IReadOnlyList<CinemaDisplayDay>> PlacesAsync(Guid show, int? city, string? search, CancellationToken ct)
    {
        var (screenings, _) = await ReadAsync(show, null, ct);
        var venues = await ReadVenuesAsync(screenings, city, null, ct);
        var term = CatalogueText.Normalize(search);
        return screenings.Where(x => venues.TryGetValue(x.Row.VenueId, out var venue)
                && (Matches(term, venue.Name, venue.Address) || Matches(term, venue.Aliases)))
            .GroupBy(x => LocalDate(x.Row.StartsAt)).OrderBy(x => x.Key)
            .Select(x => Day(x.Key, x.Select(y => venues[y.Row.VenueId]).DistinctBy(y => y.Id)
                .OrderBy(y => y.Name, CatalogueText.Comparer)
                .Select(y => new CinemaPlace(y.Id.ToString("D"), y.Name, y.Address, y.CityName, y.Image)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<CinemaSession>> SessionsAsync(Guid show, Guid place, string? date, CancellationToken ct)
    {
        var (screenings, _) = await ReadAsync(show, place, ct);
        var groups = screenings.Where(x => date is null || LocalDate(x.Row.StartsAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == date)
            .GroupBy(x => (x.ProviderKey, x.Row.ExternalFilmId, x.Row.ExternalVenueId)).ToArray();
        var sessions = new List<CinemaSession>();
        var read = 0;
        foreach (var group in groups)
        {
            var keys = group.ToDictionary(x => x.Row.ExternalId, x => x.Row.Id, StringComparer.Ordinal);
            try
            {
                var provider = providers.Get(group.Key.ProviderKey);
                var live = await provider.SessionsAsync(group.Key.ExternalFilmId, group.Key.ExternalVenueId, date, ct);
                Add(live);
                read++;
                if (date is not null && live.Count(x => keys.ContainsKey(x.Id)) < keys.Count)
                    Add(await provider.SessionsAsync(group.Key.ExternalFilmId, group.Key.ExternalVenueId, null, ct));
            }
            catch (CinemaException ex)
            {
                logger?.LogWarning("Cinema sessions request failed Provider={Provider} Status={Status}", group.Key.ProviderKey, ex.Status);
            }

            void Add(IReadOnlyList<CinemaSession> live) => sessions.AddRange(live.Where(x => keys.ContainsKey(x.Id))
                .Select(x => x with { Id = keys[x.Id].ToString("D") }));
        }
        if (groups.Length > 0 && read == 0) throw new CinemaException("دریافت سانس‌ها ممکن نیست", 503);
        return sessions.DistinctBy(x => x.Id).OrderBy(x => x.StartsAt).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<CinemaScheduleReference?> FindScheduleAsync(Guid schedule, CancellationToken ct)
    {
        foreach (var inventory in inventories)
        {
            var externalId = await inventory.Screenings.Where(x => x.Id == schedule)
                .Select(x => x.ExternalId).SingleOrDefaultAsync(ct);
            if (externalId is not null) return new(inventory.Key, externalId);
        }
        return null;
    }

    private async Task<(List<ObservedScreening> Rows, bool Partial)> ReadAsync(Guid? film, Guid? venue, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = new List<ObservedScreening>();
        var partial = false;
        var usable = false;
        foreach (var inventory in inventories)
        {
            var state = await inventory.ReadStateAsync(ct);
            if (state?.LastSuccessAt is not { } observed || observed < now.AddMinutes(-options.Value.MaxAgeMinutes))
            {
                partial = true;
                continue;
            }
            usable = true;
            partial |= state.Failed || state.PendingMappings > 0;
            var stale = state.Failed || observed < now.AddMinutes(-options.Value.StaleMinutes);
            var startsBefore = now.AddDays(options.Value.HorizonDays);
            var found = await inventory.Screenings.Where(x => x.Published && x.StartsAt > now && x.StartsAt <= startsBefore
                && (x.PurchaseStartsAt == null || x.PurchaseStartsAt <= now)
                && (x.PurchaseEndsAt == null || x.PurchaseEndsAt > now)
                && (!film.HasValue || x.FilmId == film) && (!venue.HasValue || x.VenueId == venue)).ToListAsync(ct);
            rows.AddRange(found.Select(x => new ObservedScreening(inventory.Key, x, observed, stale)));
        }
        if (!usable) throw new CatalogueUnavailableException();
        return (rows, partial);
    }

    private Task<Dictionary<Guid, CinemaVenue>> ReadVenuesAsync(List<ObservedScreening> screenings, int? city, int? province, CancellationToken ct)
    {
        var ids = screenings.Select(x => x.Row.VenueId).Distinct().ToArray();
        return db.Set<CinemaVenue>().AsNoTracking().Where(x => ids.Contains(x.Id)
            && (!city.HasValue || city == x.CityId) && (!province.HasValue || province == x.ProvinceId)).ToDictionaryAsync(x => x.Id, ct);
    }

    private sealed record ObservedScreening(string ProviderKey, DiscoveryScreening Row, DateTimeOffset ObservedAt, bool IsStale);

    private static CatalogueCategory Category(string kind) => kind == "art"
        ? new("cinema.art", "هنر و تجربه") : new("cinema", "سینما");
    private static bool Matches(string term, params string?[] values) => Matches(term, (IEnumerable<string?>)values);
    private static bool Matches(string term, IEnumerable<string?> values) => term.Length == 0
        || values.Any(value => CatalogueText.Normalize(value).Contains(term, StringComparison.Ordinal));
    private static CatalogueLocation Location(CinemaVenue venue) => new(venue.Id.ToString("D"), venue.ProvinceId, venue.ProvinceName,
        venue.CityId, venue.CityName, venue.Name, venue.Address, venue.Latitude is { } lat && venue.Longitude is { } lng ? new(lat, lng) : null);
    private static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Tehran).DateTime);
    private static CinemaDisplayDay Day(DateOnly date, IReadOnlyList<CinemaPlace> places)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var calendar = new PersianCalendar();
        string[] months = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
        var weekday = CultureInfo.GetCultureInfo("fa-IR").DateTimeFormat.GetDayName(date.DayOfWeek);
        return new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), weekday,
            $"{calendar.GetDayOfMonth(day)} {months[calendar.GetMonth(day) - 1]}", places);
    }
}
