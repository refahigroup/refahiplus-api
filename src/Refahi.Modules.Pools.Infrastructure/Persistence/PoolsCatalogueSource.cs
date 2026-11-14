using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolsCatalogueSource(PoolsDbContext db, IExternalMediaService media, IOptionsMonitor<PoolsOptions> options,
    TimeProvider clock) : ICatalogueSource
{
    public async Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query, int take, CancellationToken ct)
    {
        if (!options.CurrentValue.CatalogEnabled) throw new CatalogueUnavailableException();
        var sync = await db.PoolTicketSyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.Key == "venues", ct);
        if (sync == null || sync.CompletedAt < clock.GetUtcNow().AddDays(-7)) throw new CatalogueUnavailableException();
        var rows = await (from v in db.Venues.AsNoTracking()
            join c in db.PoolTicketContents.AsNoTracking() on v.Id equals c.VenueId
            join g in db.PoolTicketGeographies.AsNoTracking() on c.NativeCityId equals g.CityId
            where v.IsActive && c.FetchedAt >= clock.GetUtcNow().AddDays(-7)
                && (!query.ProvinceId.HasValue || g.ReferenceProvinceId == query.ProvinceId)
                && (!query.CityId.HasValue || g.ReferenceCityId == query.CityId)
            select new { V = v, C = c, G = g }).ToArrayAsync(ct);
        var normalized = CatalogueText.Normalize(query.Q);
        var items = new List<CatalogueItem>();
        foreach (var row in rows)
        {
            var v = row.V; var c = row.C; var g = row.G;
            var summary = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(c.DescriptionHtml, "<[^>]*>", " "));
            if (!CatalogueText.Normalize(v.Name + " " + summary + " " + v.State + " " + v.City).Contains(normalized)) continue;
            var point = c.Latitude.HasValue && c.Longitude.HasValue ? new CataloguePoint(c.Latitude.Value, c.Longitude.Value) : null;
            if (point?.IsValid != true) point = null;
            var item = new CatalogueItem($"pools:{v.Id:N}", "pools", "pool", v.Id.ToString("N"), v.Name,
                summary.Length > 240 ? summary[..240] : summary, null, g.ReferenceCityId, v.City, v.State, [], $"/pools/venues/{v.Id}",
                v.FetchedAt < sync.CompletedAt ? v.FetchedAt : sync.CompletedAt,
                v.FetchedAt < clock.GetUtcNow().AddSeconds(-2d * options.CurrentValue.VenueSyncSeconds),
                v.MinimumSaleMinor.HasValue ? new(v.MinimumSaleMinor.Value, "IRR", "ticket") : null,
                new("entertainment.waterpark", "استخر و پارک آبی"), [],
                [new(v.Id.ToString("N"), g.ReferenceProvinceId, v.State, g.ReferenceCityId, v.City, v.Name, c.Address, point)]);
            items.Add(CatalogueOrdering.Prepare(item, query));
        }
        var selected = items.OrderBy(x => x, CatalogueOrdering.Comparer).Take(take).ToArray();
        for (var i = 0; i < selected.Length; i++)
        {
            var venue = rows.Single(x => x.V.Id.ToString("N") == selected[i].SourceItemId).V;
            if (venue.ImageUrl != null) selected[i] = selected[i] with { ImageUrl = await media.GetPublicUrlAsync(venue.ImageUrl, new("Pools", "poolticket"), ct) };
        }
        var incomplete = await db.Venues.AnyAsync(v => v.IsActive && v.ProviderKey == "poolticket"
            && !db.PoolTicketContents.Any(c => c.VenueId == v.Id && c.FetchedAt >= clock.GetUtcNow().AddDays(-7)
                && db.PoolTicketGeographies.Any(g => g.CityId == c.NativeCityId && g.ReferenceCityId.HasValue && g.ReferenceProvinceId.HasValue)), ct);
        return new(selected, items.Count, incomplete);
    }
    public async Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct)
    {
        if (!options.CurrentValue.CatalogEnabled || !await db.PoolTicketSyncStates.AnyAsync(x => x.Key == "venues"
            && x.CompletedAt >= clock.GetUtcNow().AddDays(-7), ct)) throw new CatalogueUnavailableException();
        return await (from v in db.Venues.AsNoTracking() join c in db.PoolTicketContents on v.Id equals c.VenueId
            join g in db.PoolTicketGeographies on c.NativeCityId equals g.CityId
            where v.IsActive && g.ReferenceCityId.HasValue && g.ReferenceProvinceId.HasValue
            select new CatalogueCity(g.ReferenceCityId!.Value, g.CityName, g.StateName, g.ReferenceProvinceId!.Value)).Distinct().ToArrayAsync(ct);
    }
}
