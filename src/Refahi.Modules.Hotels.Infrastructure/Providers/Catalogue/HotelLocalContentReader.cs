using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

public sealed class HotelLocalContentReader(HotelsDbContext db, ISender sender, IPersistentExternalMedia media,
    IOptions<HotelStaticContentOptions> options) : IHotelLocalContentReader
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static HotelStaticContent? Content(HotelDiscovery row) => row.ContentJson is null ? null : JsonSerializer.Deserialize<HotelStaticContent>(row.ContentJson, Json);
    internal IQueryable<HotelDiscovery> Rows(string provider) => provider == "SnappTrip"
        ? db.Set<SnappTripDiscoveryHotel>().AsNoTracking() : db.Set<LamasoDiscoveryHotel>().AsNoTracking();
    public async Task<bool> UsesLocalAsync(Guid key, CancellationToken ct)
    {
        if (!options.Value.ReadEnabled) return false;
        var listing = await db.Set<HotelListing>().AsNoTracking().SingleOrDefaultAsync(x => x.HotelKey == key, ct);
        return listing?.ReferenceCityId is int city && options.Value.Includes(listing.Provider, city);
    }
    private async Task<Dictionary<int, CityDto>> Cities(CancellationToken ct)
    {
        var provinces = (await sender.Send(new GetProvincesQuery(ActiveOnly: true), ct)).Provinces.Select(x => x.Id).ToHashSet();
        return (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.Where(x => provinces.Contains(x.ProvinceId)).ToDictionary(x => x.Id);
    }
    public async Task<HotelDiscoveryProfile?> ProfileAsync(Guid key, CancellationToken ct)
    {
        if (!await UsesLocalAsync(key, ct)) return null;
        var cities = await Cities(ct);
        foreach (var provider in new[] { "SnappTrip", "Lamasoo" })
        {
            var row = await Rows(provider).SingleOrDefaultAsync(x => x.HotelKey == key && x.Published, ct);
            if (row is not null && cities.TryGetValue(row.ReferenceCityId, out var city)) return Map(row, provider, city);
        }
        return null;
    }
    public async Task<HotelDiscoveryProfile?> LegacyProfileAsync(long id, CancellationToken ct)
    {
        if (!options.Value.ReadEnabled) return null;
        var key = await ResolveLegacyKeyAsync(id, ct);
        return key.HasValue ? await ProfileAsync(key.Value, ct) : null;
    }
    public async Task<Guid?> ResolveLegacyKeyAsync(long id, CancellationToken ct)
    {
        var native = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await db.Set<HotelListing>().AsNoTracking().Where(x => x.Provider == "SnappTrip" && x.NativeId == native)
            .Select(x => (Guid?)x.HotelKey).SingleOrDefaultAsync(ct);
    }
    internal HotelDiscoveryProfile Map(HotelDiscovery row, string provider, CityDto city)
    {
        var content = Content(row);
        if (content is not null) content = content with { Gallery = content.Gallery.Select(x => x with { SourceUrl = null }).ToArray(), Reviews = [],
            Sections = content.Sections.ToDictionary(x => x.Key, x => x.Value with { ErrorCode = null }) };
        return new(row.HotelKey, city.Id, city.Name, city.ProvinceName, provider, row.Title, row.Description, row.Address, row.Stars,
            content?.Gallery.Select(x => media.GetUrl(x.MediaId)).ToArray() ?? [],
            content?.Facilities.Select(x => x.Title).ToArray() ?? [], content?.Policies.Cancellation,
            row.ObservedAtUtc, row.ObservedAtUtc < DateTimeOffset.UtcNow.AddHours(-options.Value.RefreshHours)
                || content?.Sections.Values.Any(x => x.Status != "unsupported" && (!x.LastSuccessAtUtc.HasValue || x.LastSuccessAtUtc < DateTimeOffset.UtcNow.AddHours(-options.Value.RefreshHours))) == true,
            city.ProvinceId, row.Latitude, row.Longitude, content, true, row.NativeId);
    }
    public async Task<HotelReviewsPage?> ReviewsAsync(Guid key, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var profile = await ProfileAsync(key, ct);
        if (profile is null) return null;
        var row = await Rows(profile.Provider).SingleAsync(x => x.HotelKey == key, ct);
        var reviews = Content(row)?.Reviews ?? [];
        var ordered = reviews.OrderByDescending(x => x.RegisteredAt).ThenBy(x => x.Id, StringComparer.Ordinal);
        return new(ordered.Skip((page - 1) * size).Take(size).ToArray(), reviews.Count, page, size,
            profile.Content?.Sections.GetValueOrDefault("reviews")?.Status ?? "pending");
    }
    private static void ValidatePage(int page, int size)
    {
        if (page is < 1 or > 10000 || size is < 1 or > 50) throw new FluentValidation.ValidationException("صفحه و تعداد نتایج معتبر نیست.");
    }
    public async Task<IReadOnlyList<HotelContentReadiness>> ReadinessAsync(CancellationToken ct)
    {
        var result = new List<HotelContentReadiness>();
        foreach (var provider in new[] { "SnappTrip", "Lamasoo" })
        {
            var rows = await Rows(provider).Where(x => x.Published).ToArrayAsync(ct);
            foreach (var group in rows.GroupBy(x => x.ReferenceCityId))
            {
                var ready = 0; var missing = 0;
                foreach (var row in group)
                {
                    var content = Content(row);
                    var absent = content?.Gallery.Count(x => !media.Exists(x.MediaId)) ?? 0;
                    missing += absent;
                    if (row.ContentReady == true && content?.IsReady == true && absent == 0
                        && content.Sections.Values.All(x => x.Status == "unsupported" || x.LastSuccessAtUtc >= DateTimeOffset.UtcNow.AddHours(-options.Value.RefreshHours))) ready++;
                }
                result.Add(new(provider, group.Key, group.Count(), ready, missing, ready == group.Count()));
            }
        }
        return result;
    }
    public async Task<IReadOnlyList<HotelDiscoveryProfile>?> ProviderHotelsAsync(string provider, string nativeCityId, CancellationToken ct)
    {
        if (!options.Value.ReadEnabled) return null;
        var referenceCity = provider == "Lamasoo"
            ? await db.Set<LamasoCityMapping>().AsNoTracking().Where(x => x.ProviderCityId == nativeCityId).Select(x => (int?)x.CityId).SingleOrDefaultAsync(ct)
            : await db.Set<SnappTripCityMapping>().AsNoTracking().Where(x => x.ProviderCityId == nativeCityId).Select(x => (int?)x.CityId).SingleOrDefaultAsync(ct);
        if (!referenceCity.HasValue || !options.Value.Includes(provider, referenceCity.Value)) return null;
        var cities = await Cities(ct);
        if (!cities.TryGetValue(referenceCity.Value, out var city)) return [];
        return (await Rows(provider).Where(x => x.NativeCityId == nativeCityId && x.Published).ToArrayAsync(ct)).Select(x => Map(x, provider, city)).ToArray();
    }
    private sealed record Candidate(Guid Key, string Provider, string Title, double? Rating, double? Distance);
    public async Task<HotelLocalSearchPage> SearchAsync(HotelLocalSearch query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var origin = query.Latitude.HasValue && query.Longitude.HasValue ? new CataloguePoint(query.Latitude.Value, query.Longitude.Value) : null;
        if (query.Sort is not ("title" or "rating" or "distance") || query.Sort == "distance" && origin is null
            || query.Latitude.HasValue != query.Longitude.HasValue || origin is { IsValid: false }
            || query.MinRating is { } rating && (!double.IsFinite(rating) || rating < 0 || rating > 10)
            || query.Stars?.Any(x => x is < 0 or > 5) == true || query.Query?.Length > 500
            || query.Facilities?.Length > 30 || query.Accommodations?.Length > 30)
            throw new FluentValidation.ValidationException("فیلترهای جست‌وجوی هتل معتبر نیست.");
        if (!options.Value.ReadEnabled) throw new CatalogueUnavailableException();
        var cities = await Cities(ct);
        if (query.CityId.HasValue && (!cities.TryGetValue(query.CityId.Value, out var requested) || query.ProvinceId.HasValue && requested.ProvinceId != query.ProvinceId))
            throw new FluentValidation.ValidationException("شهر مقصد معتبر نیست.");
        var selectedCities = cities.Values.Where(x => (!query.CityId.HasValue || x.Id == query.CityId) && (!query.ProvinceId.HasValue || x.ProvinceId == query.ProvinceId)).ToArray();
        var comparer = Comparer<Candidate>.Create((a, b) => {
            var primary = query.Sort == "rating" ? Nullable.Compare(b.Rating, a.Rating)
                : query.Sort == "distance" ? (a.Distance ?? double.MaxValue).CompareTo(b.Distance ?? double.MaxValue) : 0;
            return primary != 0 ? primary : CatalogueOrdering.Compare(null, a.Title, a.Provider, a.Key.ToString("N"), null, b.Title, b.Provider, b.Key.ToString("N"));
        });
        var candidates = new SortedSet<Candidate>(comparer); var count = 0;
        var window = checked(query.Page * query.PageSize);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        foreach (var provider in new[] { "SnappTrip", "Lamasoo" })
        {
            var ids = selectedCities.Where(x => options.Value.Includes(provider, x.Id)).Select(x => x.Id).ToArray();
            if (ids.Length == 0) continue;
            var rows = Rows(provider).Where(x => x.Published && ids.Contains(x.ReferenceCityId));
            foreach (var word in CatalogueText.Normalize(query.Query).Split(' ', StringSplitOptions.RemoveEmptyEntries)) rows = rows.Where(x => x.SearchText.Contains(word));
            if (query.Stars is { Length: > 0 }) rows = rows.Where(x => query.Stars.Contains(x.Stars));
            if (query.Accommodations is { Length: > 0 }) rows = rows.Where(x => query.Accommodations.Contains(x.AccommodationType!));
            if (query.MinRating.HasValue) rows = rows.Where(x => x.Rating >= query.MinRating);
            foreach (var facility in query.Facilities ?? []) { var key = CatalogueText.Normalize(facility); rows = rows.Where(x => x.FacilityKeys != null && x.FacilityKeys.Contains(key)); }
            count += await rows.CountAsync(ct);
            var scan = query.Sort == "title" ? rows.OrderBy(x => EF.Functions.Collate(x.SortTitle, "C")).ThenBy(x => x.HotelKey).Take(window)
                : query.Sort == "rating" ? rows.OrderByDescending(x => x.Rating ?? -1).ThenBy(x => EF.Functions.Collate(x.SortTitle, "C")).ThenBy(x => x.HotelKey).Take(window) : rows;
            await foreach (var row in scan.Select(x => new { x.HotelKey, x.SortTitle, x.Rating, x.Latitude, x.Longitude }).AsAsyncEnumerable().WithCancellation(ct))
            {
                var point = row.Latitude.HasValue && row.Longitude.HasValue ? new CataloguePoint(row.Latitude.Value, row.Longitude.Value) : null;
                candidates.Add(new(row.HotelKey, provider, row.SortTitle, row.Rating, CatalogueOrdering.Distance(origin, point)));
                if (candidates.Count > window) candidates.Remove(candidates.Max!);
            }
        }
        var page = candidates.Skip((query.Page - 1) * query.PageSize).ToArray();
        var profiles = new Dictionary<Guid, HotelDiscoveryProfile>();
        foreach (var group in page.GroupBy(x => x.Provider))
        {
            var keys = group.Select(x => x.Key).ToArray();
            foreach (var row in await Rows(group.Key).Where(x => keys.Contains(x.HotelKey)).ToArrayAsync(ct)) profiles[row.HotelKey] = Map(row, group.Key, cities[row.ReferenceCityId]);
        }
        await tx.CommitAsync(ct);
        return new(page.Select(x => profiles[x.Key]).ToArray(), count, query.Page, query.PageSize);
    }
}
