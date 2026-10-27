using MediatR;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.Catalogue.Application.Contracts;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

public sealed class HotelDiscoveryReader(HotelsDbContext db, ISender sender, IHotelCatalogueProviderRegistry providers, IOptions<HotelDiscoveryOptions> options, ILogger<HotelDiscoveryReader>? logger = null,
    IOptions<HotelStaticContentOptions>? staticOptions = null, IHotelLocalContentReader? local = null) : IHotelDiscoveryReader
{
    private async Task<(string Provider, string NativeCity, CityDto City)[]> Coverage(CancellationToken ct)
    {
        var activeProvinces = (await sender.Send(new GetProvincesQuery(ActiveOnly:true), ct)).Provinces.Select(x=>x.Id).ToHashSet();
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.Where(x=>activeProvinces.Contains(x.ProvinceId)).ToDictionary(x => x.Id);
        var active = providers.Active.Select(x => x.Name).ToHashSet();
        if (staticOptions?.Value.ReadEnabled == true)
            foreach (var scope in staticOptions.Value.ReadScopes) active.Add(scope.Split(':')[0]);
        var result = new List<(string, string, CityDto)>();
        if (active.Contains("SnappTrip")) foreach (var m in await db.Set<SnappTripCityMapping>().AsNoTracking().ToListAsync(ct)) if (cities.TryGetValue(m.CityId, out var c)) result.Add(("SnappTrip", m.ProviderCityId, c));
        if (active.Contains("Lamasoo")) foreach (var m in await db.Set<LamasoCityMapping>().AsNoTracking().Where(x => x.IsEnabled).ToListAsync(ct)) if (cities.TryGetValue(m.CityId, out var c)) result.Add(("Lamasoo", m.ProviderCityId, c));
        return result.ToArray();
    }
    public async Task<IReadOnlyList<HotelCityDto>> CitiesAsync(CancellationToken ct) => (await Coverage(ct)).Select(x => x.City).DistinctBy(x => x.Id).Select(x => new HotelCityDto(x.Id, x.Name, x.NameEn, x.ProvinceId, x.ProvinceName)).ToArray();
    public Task<HotelDiscoveryPage> SearchAsync(string? query, int? cityId, int take, CancellationToken ct) => SearchAsync(new HotelDiscoverySearch(query, cityId),take,ct);

    private sealed record Candidate(Guid Key, string Provider, string SortTitle, double? Distance);
    private static readonly IComparer<Candidate> CandidateComparer = Comparer<Candidate>.Create((a,b)=>CatalogueOrdering.Compare(a.Distance,a.SortTitle,"hotels",a.Key.ToString("N"),b.Distance,b.SortTitle,"hotels",b.Key.ToString("N")));

    public async Task<HotelDiscoveryPage> SearchAsync(HotelDiscoverySearch query, int take, CancellationToken ct)
    {
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
        if (providers.Active.Count == 0 && staticOptions?.Value.ReadEnabled != true) throw new CatalogueUnavailableException();
        var coverage = (await Coverage(ct)).Where(x => (!query.CityId.HasValue || x.City.Id == query.CityId) && (!query.ProvinceId.HasValue || x.City.ProvinceId == query.ProvinceId)).ToArray();
        if (coverage.Length == 0) return new([], 0, false);
        var origin = query.Latitude.HasValue && query.Longitude.HasValue ? new CataloguePoint(query.Latitude.Value,query.Longitude.Value) : null;
        var selected = new SortedSet<Candidate>(CandidateComparer);
        var partial = false; var ready = 0; var count = 0; var located = 0;
        var watch = Stopwatch.StartNew();
        // One snapshot across candidate selection, count and hydration; no supplier/Media HTTP inside it.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        foreach (var group in coverage.GroupBy(x => x.Provider))
        {
            var nativeCities = group.Select(x => x.NativeCity).Distinct().ToArray();
            var min = DateTimeOffset.UtcNow.AddDays(-options.Value.MaxAgeDays);
            var retainedCities = group.Where(x => staticOptions?.Value.Includes(x.Provider, x.City.Id) == true).Select(x => x.NativeCity).ToArray();
            var states = group.Key == "SnappTrip" ? await db.Set<SnappTripDiscoverySyncState>().AsNoTracking().Where(x => nativeCities.Contains(x.NativeCityId)).Select(x => new { x.NativeCityId, x.LastSuccessAtUtc }).ToArrayAsync(ct) : await db.Set<LamasoDiscoverySyncState>().AsNoTracking().Where(x => nativeCities.Contains(x.NativeCityId)).Select(x => new { x.NativeCityId, x.LastSuccessAtUtc }).ToArrayAsync(ct);
            ready += states.Count(x => x.LastSuccessAtUtc >= min || retainedCities.Contains(x.NativeCityId) && x.LastSuccessAtUtc.HasValue); partial |= states.Count(x => x.LastSuccessAtUtc >= min || retainedCities.Contains(x.NativeCityId) && x.LastSuccessAtUtc.HasValue) != nativeCities.Length;
            IQueryable<HotelDiscovery> rows = group.Key == "SnappTrip" ? db.Set<SnappTripDiscoveryHotel>().AsNoTracking() : db.Set<LamasoDiscoveryHotel>().AsNoTracking();
            rows = rows.Where(x => nativeCities.Contains(x.NativeCityId) && x.Published && (x.ObservedAtUtc >= min || retainedCities.Contains(x.NativeCityId)));
            foreach (var word in CatalogueText.Normalize(query.Query).Split(' ', StringSplitOptions.RemoveEmptyEntries)) rows = rows.Where(x => x.SearchText.Contains(word));
            count += await rows.CountAsync(ct);
            // shortcut: nearby scans lightweight matches; move ranking to SQL if measured source latency exceeds its deadline.
            var candidates = origin is null ? rows.OrderBy(x => EF.Functions.Collate(x.SortTitle,"C")).ThenBy(x=>x.HotelKey).Take(take) : rows;
            await foreach (var row in candidates.Select(x=>new { x.HotelKey,x.SortTitle,x.Latitude,x.Longitude }).AsAsyncEnumerable().WithCancellation(ct))
            {
                var point = row.Latitude.HasValue && row.Longitude.HasValue ? new CataloguePoint(row.Latitude.Value,row.Longitude.Value) : null;
                if(point is { IsValid:true }) located++;
                selected.Add(new(row.HotelKey,group.Key,row.SortTitle,CatalogueOrdering.Distance(origin,point)));
                if(selected.Count>take) selected.Remove(selected.Max!);
            }
        }
        if (ready == 0) throw new CatalogueUnavailableException();
        var selectionMs = watch.Elapsed.TotalMilliseconds;
        var profiles = new Dictionary<Guid,HotelDiscoveryProfile>();
        foreach(var group in selected.GroupBy(x=>x.Provider))
        {
            var keys=group.Select(x=>x.Key).ToArray();
            IQueryable<HotelDiscovery> rows = group.Key == "SnappTrip" ? db.Set<SnappTripDiscoveryHotel>().AsNoTracking() : db.Set<LamasoDiscoveryHotel>().AsNoTracking();
            foreach(var row in await rows.Where(x=>keys.Contains(x.HotelKey)).ToListAsync(ct))
                profiles.Add(row.HotelKey,local is not null && staticOptions?.Value.Includes(group.Key,row.ReferenceCityId) == true
                    ? await local.ProfileAsync(row.HotelKey,ct) ?? Map(row,group.Key,coverage.First(x=>x.Provider==group.Key && x.NativeCity==row.NativeCityId).City)
                    : Map(row,group.Key,coverage.First(x=>x.Provider==group.Key && x.NativeCity==row.NativeCityId).City));
        }
        await tx.CommitAsync(ct);
        logger?.LogInformation("Hotel discovery read Nearby={Nearby} Matches={Matches} LocatedCandidates={Located} SelectionMs={SelectionMs} TotalMs={TotalMs} Partial={Partial}",origin is not null,count,located,selectionMs,watch.Elapsed.TotalMilliseconds,partial);
        return new(selected.Select(x=>profiles[x.Key]).ToArray(),partial?null:count,partial);
    }
    public async Task<HotelDiscoveryProfile?> ProfileAsync(Guid key, CancellationToken ct)
    {
        if (local is not null && await local.UsesLocalAsync(key,ct)) return await local.ProfileAsync(key,ct);
        var coverage = await Coverage(ct);
        foreach (var group in coverage.GroupBy(x => x.Provider))
        {
            HotelDiscovery? row = group.Key == "SnappTrip" ? await db.Set<SnappTripDiscoveryHotel>().AsNoTracking().SingleOrDefaultAsync(x => x.HotelKey == key, ct) : await db.Set<LamasoDiscoveryHotel>().AsNoTracking().SingleOrDefaultAsync(x => x.HotelKey == key, ct);
            if (row is null || !row.Published) continue; var c = group.FirstOrDefault(x => x.NativeCity == row.NativeCityId); if (c.City is null) continue;
            if (row.ObservedAtUtc < DateTimeOffset.UtcNow.AddDays(-options.Value.MaxAgeDays)) throw new CatalogueUnavailableException();
            return Map(row, group.Key, c.City);
        }
        return null;
    }
    private HotelDiscoveryProfile Map(HotelDiscovery row, string provider, CityDto city) => new(row.HotelKey, city.Id, city.Name, city.ProvinceName, provider, row.Title, row.Description, row.Address, row.Stars, System.Text.Json.JsonSerializer.Deserialize<string[]>(row.ImagesJson) ?? [], System.Text.Json.JsonSerializer.Deserialize<string[]>(row.FacilitiesJson) ?? [], row.Policies, row.ObservedAtUtc, row.ObservedAtUtc < DateTimeOffset.UtcNow.AddHours(-options.Value.RefreshHours), city.ProvinceId, row.Latitude, row.Longitude);
}
