using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Persistence;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Cinema.Infrastructure.Workers;

internal sealed class ITicketDiscoverySync(CinemaDbContext db, ITicketCinemaProvider provider, ICinemaMutationLock gate,
    ISender sender, IExternalMediaService media, TimeProvider clock, IOptions<CinemaDiscoveryOptions> options,
    ILogger<ITicketDiscoverySync> logger) : ICinemaDiscoverySync
{
    private static readonly Guid SyncLock = new(SHA256.HashData("Cinema discovery iticket"u8)[..16]);

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var lease = await gate.AcquireAsync(SyncLock, ct);
            var now = clock.GetUtcNow();
            var state = await db.Set<ITicketDiscoveryState>().SingleOrDefaultAsync(ct);
            if (state is not null && state.LastAttemptAt > now.AddMinutes(-options.Value.RefreshMinutes)) return;
            try
            {
                var source = await provider.ReadDiscoveryAsync(now, options.Value.HorizonDays, ct);
                var references = await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct);
                var images = await RegisterMediaAsync(source, ct);
                await PublishAsync(source, references, images, now, state, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Cinema discovery sync failed Provider={Provider} ErrorType={ErrorType}", provider.Key, ex.GetType().Name);
                db.ChangeTracker.Clear();
                var failed = await db.Set<ITicketDiscoveryState>().SingleOrDefaultAsync(ct);
                if (failed is null) db.Add(failed = new ITicketDiscoveryState());
                failed.LastAttemptAt = now;
                failed.Failed = true;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError("Cinema discovery worker failed Provider={Provider} ErrorType={ErrorType}", provider.Key, ex.GetType().Name);
        }
    }

    private async Task PublishAsync(ITicketDiscoveryFeed source, GetCitiesResponse references,
        IReadOnlyDictionary<string, string> images, DateTimeOffset now, ITicketDiscoveryState? state, CancellationToken ct)
    {
        var mappings = (await db.Set<ITicketMapping>().ToListAsync(ct)).ToDictionary(x => (x.Kind, x.ExternalId), x => x.CanonicalId);
        var films = await db.Set<CinemaFilm>().Include(x => x.Credits).ToDictionaryAsync(x => x.Id, ct);
        var venues = await db.Set<CinemaVenue>().ToDictionaryAsync(x => x.Id, ct);
        var people = await db.Set<CinemaPerson>().ToDictionaryAsync(x => x.Id, ct);
        var geographies = await db.Set<ITicketGeography>().ToDictionaryAsync(x => x.ExternalCityId, ct);
        var screenings = await db.Set<ITicketScreening>().ToDictionaryAsync(x => x.ExternalId, ct);
        var referenceByName = references.Cities.ToLookup(x => (CatalogueText.Normalize(x.Name), CatalogueText.Normalize(x.ProvinceName)));
        var linkedCities = new Dictionary<int, (int CityId, string CityName, int ProvinceId, string ProvinceName)>();
        var referenceById = references.Cities.ToDictionary(x => x.Id);
        foreach (var city in source.Cities)
        {
            if (!geographies.TryGetValue(city.Id, out var geo))
            {
                geo = new() { ExternalCityId = city.Id };
                db.Add(geo);
            }
            var matches = referenceByName[(CatalogueText.Normalize(city.Name), CatalogueText.Normalize(city.Province))].ToArray();
            var matched = geo.ExternalProvinceId == city.ProvinceId && geo.CityId is { } id
                && referenceById.TryGetValue(id, out var existing) && existing.ProvinceId == geo.ProvinceId
                ? existing : matches.Length == 1 ? matches[0] : null;
            geo.ExternalProvinceId = city.ProvinceId;
            geo.CityId = matched?.Id;
            geo.ProvinceId = matched?.ProvinceId;
            if (matched is not null) linkedCities[city.Id] = (matched.Id, matched.Name, matched.ProvinceId, matched.ProvinceName);
        }

        var pending = 0;
        var activeFilms = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var publications = new List<(Guid Id, ITicketFilm Item)>();
        foreach (var item in source.Films.OrderBy(x => x.Show.Id, StringComparer.Ordinal))
        {
            var show = item.Show;
            var name = CatalogueText.Normalize(show.Title);
            if (!mappings.TryGetValue(("film", show.Id), out var filmId))
            {
                if (films.Values.Any(x => CatalogueText.Normalize(x.Title) == name
                    && (x.ReleaseYear == item.ReleaseYear || x.ReleaseYear is null || item.ReleaseYear is null)))
                {
                    pending++;
                    logger.LogWarning("Cinema film mapping pending Provider={Provider} ExternalId={ExternalId}", provider.Key, show.Id);
                    continue;
                }
                var created = new CinemaFilm { Title = show.Title, ReleaseYear = item.ReleaseYear };
                db.Add(created);
                films.Add(created.Id, created);
                filmId = Map("film", show.Id, created.Id);
            }
            activeFilms.Add(show.Id, filmId);
            publications.Add((filmId, item));
        }

        foreach (var group in publications.GroupBy(x => x.Id))
        {
            var film = films[group.Key];
            var details = group.Select(x => x.Item).ToArray();
            var item = details.FirstOrDefault(x => x.Show.Title == film.Title) ?? details[0];
            var show = item.Show;
            film.Title = show.Title;
            film.Kind = show.Kind;
            film.Aliases = film.Aliases.Union(details.SelectMany(x => x.Aliases.Append(x.Show.Title))
                .Where(x => x != film.Title), StringComparer.Ordinal).ToArray();
            film.Genres = details.SelectMany(x => x.Show.Genres ?? []).Select(genre => genre.Name).Distinct().ToArray();
            film.SummaryText = show.SummaryText;
            film.SummaryHtml = show.SummaryHtml;
            film.DescriptionHtml = show.DescriptionHtml;
            film.Poster = Image(images, show.Poster) ?? film.Poster;
            film.Cover = Image(images, show.Cover) ?? film.Cover;
            film.DurationMinutes = show.DurationMinutes;
            film.AgeGroup = show.AgeGroup;
            film.ReleaseYear = item.ReleaseYear ?? film.ReleaseYear;

            if (details.All(x => x.Credits is null)) continue;
            var existingCredits = film.Credits.ToDictionary(x => x.PersonId);
            var retained = new HashSet<Guid>();
            var credits = details.SelectMany(x => (x.Credits ?? []).Select(credit => (x.Show.Id, Credit: credit)));
            foreach (var (entry, order) in credits.Select((entry, index) => (entry, index)))
            {
                var credit = entry.Credit;
                var artist = credit.Artist;
                var artistId = string.IsNullOrWhiteSpace(artist.Id)
                    ? entry.Id + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CatalogueText.Normalize(artist.Name))))
                    : artist.Id;
                if (!mappings.TryGetValue(("person", artistId), out var personId))
                {
                    var created = new CinemaPerson();
                    db.Add(created);
                    people.Add(created.Id, created);
                    personId = Map("person", artistId, created.Id);
                }
                if (!retained.Add(personId)) continue;
                var person = people[personId];
                if (person.Name.Length > 0 && person.Name != artist.Name)
                    person.Aliases = person.Aliases.Append(person.Name).Distinct(StringComparer.Ordinal).ToArray();
                person.Name = artist.Name;
                person.Aliases = person.Aliases.Union(credit.Aliases, StringComparer.Ordinal).ToArray();
                person.Portrait = Image(images, artist.Portrait) ?? person.Portrait;
                if (!existingCredits.TryGetValue(personId, out var relation))
                {
                    relation = new() { FilmId = film.Id, PersonId = personId, Person = person };
                    db.Add(relation);
                }
                relation.Role = credit.Role ?? relation.Role;
                relation.SortOrder = order;
            }
            db.RemoveRange(existingCredits.Values.Where(x => !retained.Contains(x.PersonId)));
        }

        var activeVenues = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var updatedVenues = new HashSet<Guid>();
        var unmapped = 0;
        foreach (var item in source.Venues.OrderBy(x => x.Place.Id, StringComparer.Ordinal))
        {
            (int CityId, string CityName, int ProvinceId, string ProvinceName)? city =
                linkedCities.TryGetValue(item.CityId.GetValueOrDefault(), out var linked) ? linked : null;
            var place = item.Place;
            if (!mappings.TryGetValue(("venue", place.Id), out var venueId))
            {
                var name = CatalogueText.Normalize(place.Title);
                var address = CatalogueText.Normalize(place.Address);
                if (venues.Values.Any(x => x.CityId == city?.CityId && CatalogueText.Normalize(x.Name) == name
                    && (string.IsNullOrEmpty(address) || string.IsNullOrWhiteSpace(x.Address) || CatalogueText.Normalize(x.Address) == address)))
                {
                    pending++;
                    logger.LogWarning("Cinema venue mapping pending Provider={Provider} ExternalId={ExternalId}", provider.Key, place.Id);
                    continue;
                }
                var created = new CinemaVenue();
                db.Add(created);
                venues.Add(created.Id, created);
                venueId = Map("venue", place.Id, created.Id);
            }
            var venue = venues[venueId];
            activeVenues.Add(place.Id, venue.Id);
            if (!updatedVenues.Add(venueId))
            {
                venue.Aliases = venue.Aliases.Union(item.Aliases.Append(place.Title)
                    .Where(name => name != venue.Name), StringComparer.Ordinal).ToArray();
                continue;
            }
            if (city is null) unmapped++;
            venue.Name = place.Title;
            venue.Aliases = venue.Aliases.Union(item.Aliases, StringComparer.Ordinal).ToArray();
            venue.Address = place.Address;
            venue.Image = Image(images, place.Cover) ?? venue.Image;
            venue.CityId = city?.CityId;
            venue.CityName = city?.CityName ?? place.City;
            venue.ProvinceId = city?.ProvinceId;
            venue.ProvinceName = city?.ProvinceName;
            venue.Latitude = item.Point?.Latitude;
            venue.Longitude = item.Point?.Longitude;
        }

        foreach (var screening in screenings.Values) screening.Published = false;
        foreach (var item in source.Sessions)
        {
            if (!activeFilms.TryGetValue(item.FilmId, out var film) || !activeVenues.TryGetValue(item.PlaceId, out var venue)) continue;
            if (!screenings.TryGetValue(item.Id, out var row))
            {
                row = new() { ExternalId = item.Id };
                db.Add(row);
            }
            row.FilmId = film;
            row.VenueId = venue;
            row.ExternalFilmId = item.FilmId;
            row.ExternalVenueId = item.PlaceId;
            row.StartsAt = item.StartsAt;
            row.PurchaseStartsAt = item.PurchaseStartsAt;
            row.PurchaseEndsAt = item.PurchaseEndsAt;
            row.Status = item.Status;
            row.Published = item.Available;
        }
        if (state is null) db.Add(state = new ITicketDiscoveryState());
        state.LastAttemptAt = now;
        state.LastSuccessAt = clock.GetUtcNow();
        state.Failed = false;
        state.PendingMappings = pending;
        state.UnmappedVenues = unmapped;
        // EF saves the publication and its state together, after all provider reads completed.
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Cinema discovery published Provider={Provider} Films={Films} Venues={Venues} Screenings={Screenings} UnmappedVenues={UnmappedVenues} PendingMappings={PendingMappings} DurationMs={DurationMs}",
            provider.Key, activeFilms.Count, activeVenues.Count, source.Sessions.Length, unmapped, pending,
            (clock.GetUtcNow() - now).TotalMilliseconds);

        Guid Map(string kind, string externalId, Guid canonicalId)
        {
            db.Add(new ITicketMapping { Kind = kind, ExternalId = externalId, CanonicalId = canonicalId });
            mappings.Add((kind, externalId), canonicalId);
            return canonicalId;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> RegisterMediaAsync(ITicketDiscoveryFeed source, CancellationToken ct)
    {
        var urls = source.Films.SelectMany(x => (x.Credits ?? []).Select(a => a.Artist.Portrait).Append(x.Show.Poster).Append(x.Show.Cover))
            .Concat(source.Venues.Select(x => x.Place.Cover)).Select(MediaInput).OfType<string>().Distinct().ToArray();
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var batch in urls.Chunk(100))
            {
                var result = await media.GetPublicUrlsAsync(batch, new("Cinema", provider.Key), ct);
                if (result.Count != batch.Length) continue;
                foreach (var (input, output) in batch.Zip(result))
                    if (!string.IsNullOrWhiteSpace(output)) images[input] = output;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Cinema discovery media registration failed Provider={Provider} ErrorType={ErrorType}", provider.Key, ex.GetType().Name);
        }
        return images;
    }

    private static string? Image(IReadOnlyDictionary<string, string> images, string? source) =>
        MediaInput(source) is { } input ? images.GetValueOrDefault(input) : null;

    private static string? MediaInput(string? source) => source?.Length <= 8192 && Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;
}
