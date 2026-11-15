using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Options;

namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar;

internal sealed class AabsarCatalogueReader(PoolsDbContext db, TimeProvider clock,
    IOptionsMonitor<AabsarOptions> options, IExternalMediaService media) : IAabsarCatalogue
{
    private const string Key = "aabsar";
    private static readonly AabsarAddress Address = new("اصفهان", "اصفهان", AabsarContent.Address, new(32.555924, 51.671920));

    public async Task<AabsarSeller?> VenueAsync(CancellationToken ct)
    {
        if (!options.CurrentValue.CatalogueEnabled) return null;
        return new(Key, Key, AabsarContent.Name, ["استخر", "تفریحات آبی", "پارک آبی"],
            await ImageAsync(AabsarContent.Logo, ct), await ImageAsync(AabsarContent.Cover, ct), AabsarContent.Description, Address);
    }

    public async Task<Guid?> ResolveLegacyProductAsync(string key, CancellationToken ct)
    {
        if (!await IsPublishedAsync(ct)) return null;
        return await db.AabsarProducts.AsNoTracking().Where(x => x.EventId == key && x.IsActive)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    }

    public async Task<AabsarProductView?> ProductAsync(string key, CancellationToken ct)
    {
        if (!Guid.TryParse(key, out var id) || !await IsPublishedAsync(ct)) return null;
        var product = await db.AabsarProducts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (product is null) return null;
        var showtimes = await Showtimes().Where(x => x.ProductId == id).OrderBy(x => x.StartsAt).ToArrayAsync(ct);
        return Map(product, showtimes, await ImageAsync(AabsarContent.ProductImage, ct));
    }

    public async Task<AabsarPage<AabsarProductView>> ProductsAsync(string? search, int page, int size, CancellationToken ct)
    {
        if (!await IsPublishedAsync(ct)) return new([], page, size, 0, 0);
        var showtimes = Showtimes();
        var query = db.AabsarProducts.AsNoTracking().Where(p => p.IsActive && showtimes.Any(s => s.ProductId == p.Id));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Title.Contains(search.Trim()));
        var total = await query.CountAsync(ct);
        var products = await query.OrderBy(p => p.Title).ThenBy(p => p.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var ids = products.Select(x => x.Id).ToArray();
        var rows = (await showtimes.Where(s => ids.Contains(s.ProductId)).OrderBy(s => s.StartsAt).ToArrayAsync(ct))
            .ToLookup(x => x.ProductId);
        var image = products.Length == 0 ? null : await ImageAsync(AabsarContent.ProductImage, ct);
        return new(products.Select(p => Map(p, rows[p.Id], image)).ToArray(), page, size, total, (int)Math.Ceiling((double)total / size));
    }

    private Task<bool> IsPublishedAsync(CancellationToken ct)
        => options.CurrentValue.CatalogueEnabled
            ? db.AabsarVenues.AsNoTracking().AnyAsync(v => v.PublishedAt >= clock.GetUtcNow().AddDays(-7), ct)
            : Task.FromResult(false);

    private IQueryable<AabsarShowtime> Showtimes()
    {
        var now = clock.GetUtcNow();
        return db.AabsarShowtimes.AsNoTracking().Where(x => x.IsActive && x.Capacity > 0 && x.StartsAt > now);
    }

    private async Task<string?> ImageAsync(string url, CancellationToken ct)
        => await media.GetPublicUrlAsync(url, new("Pools", Key), ct);

    private static AabsarProductView Map(AabsarProduct p, IEnumerable<AabsarShowtime> rows, string? image)
        => new(Key, Key, p.Id.ToString("D"), p.Title, "", image, [], Address,
            rows.Select(s => new AabsarOffer(Key, Key, p.Id.ToString("D"), s.Id.ToString("D"), s.Title,
                s.VendorName, s.StartsAt, s.Capacity, "entertainment.waterpark",
                [new("adult", "بزرگسال", s.AdultPrice, s.AdultOldPrice, s.AdultPrice > 0),
                 new("child", "کودک", s.ChildPrice, s.ChildOldPrice, s.ChildPrice > 0)])).ToArray());
}
