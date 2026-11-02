using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

public sealed class EntertainmentDiscoveryReader(EntertainmentDbContext db, ISender sender,
    IOptions<TouristPanelOptions> providerOptions, IOptions<EntertainmentsOptions> moduleOptions)
    : IEntertainmentDiscovery, IEntertainmentContentReader, IEntertainmentProgramIdentity, IEntertainmentLocationMappings
{
    private TouristPanelOptions Provider => providerOptions.Value;
    private DateTimeOffset Cutoff => DateTimeOffset.UtcNow.AddDays(-moduleOptions.Value.MaxDiscoveryAgeDays);
    private async Task<Dictionary<string, CityDto>> Geography(CancellationToken ct)
    {
        var provinces = (await sender.Send(new GetProvincesQuery(true), ct)).Provinces.Select(x => x.Id).ToHashSet();
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.Where(x => provinces.Contains(x.ProvinceId)).ToDictionary(x => x.Id);
        var mappings = await db.LocationMappings.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey).ToListAsync(ct);
        return mappings.Where(x => cities.ContainsKey(x.CityId)).ToDictionary(x => x.LocationCode, x => cities[x.CityId]);
    }

    private async Task<(IQueryable<TouristPanelProgram> Rows, Dictionary<string, CityDto> Geo, bool Partial)> Scope(
        string? search, string? location, string? category, int? province, int? city, CancellationToken ct)
    {
        if (!Provider.Enabled) throw new EntertainmentUnavailableException();
        var cutoff = Cutoff;
        var state = await db.SyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.AccountKey == Provider.AccountKey, ct);
        if (state?.LastSuccessUtc is null || state.LastSuccessUtc < cutoff) throw new EntertainmentUnavailableException();
        var geo = await Geography(ct);
        var mapped = await db.LocationMappings.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey).Select(x => x.LocationCode).ToArrayAsync(ct);
        var inactive = mapped.Except(geo.Keys).ToArray();
        var rows = db.Programs.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey && x.IsListed && !inactive.Contains(x.LocationCode!));
        if (province.HasValue || city.HasValue)
        {
            var codes = geo.Where(x => (!province.HasValue || x.Value.ProvinceId == province) && (!city.HasValue || x.Value.Id == city)).Select(x => x.Key).ToArray();
            rows = rows.Where(x => codes.Contains(x.LocationCode!));
        }
        if (!string.IsNullOrWhiteSpace(location)) rows = rows.Where(x => x.LocationCode == location);
        if (!string.IsNullOrWhiteSpace(category))
            rows = int.TryParse(category, out var code) ? rows.Where(x => x.CategoryCode == code) : rows.Where(x => false);
        var pending = await rows.AnyAsync(x => x.ObservedAtUtc == null || x.ObservedAtUtc < cutoff || x.ListedAtUtc < cutoff, ct);
        rows = rows.Where(x => x.ObservedAtUtc >= cutoff && x.ListedAtUtc >= cutoff);
        if (pending && !await rows.AnyAsync(ct)) throw new EntertainmentUnavailableException();
        foreach (var word in CatalogueText.Normalize(search).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var codes = geo.Where(x => CatalogueText.Normalize(x.Value.Name + " " + x.Value.ProvinceName).Contains(word, StringComparison.Ordinal)).Select(x => x.Key).ToArray();
            rows = rows.Where(x => x.SearchText.Contains(word) || codes.Contains(x.LocationCode!));
        }
        return (rows, geo, pending || state.Status != "Ready");
    }
    private static IOrderedQueryable<TouristPanelProgram> Ordered(IQueryable<TouristPanelProgram> rows) =>
        rows.OrderBy(x => EF.Functions.Collate(x.SortTitle, "C")).ThenBy(x => x.ProgramKey);

    public async Task<EntertainmentDiscoveryPage> SearchAsync(EntertainmentDiscoverySearch query, int take, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var scope = await Scope(query.Q, null, null, query.ProvinceId, query.CityId, ct);
        var count = await scope.Rows.CountAsync(ct);
        var rows = await Ordered(scope.Rows).Take(take).ToListAsync(ct);
        var items = rows.Select(x => {
            var city = x.LocationCode is { } code ? scope.Geo.GetValueOrDefault(code) : null;
            return new EntertainmentDiscoveryItem(x.ProgramKey, EntertainmentPresentation.Text(x.Title)!, Summary(EntertainmentPresentation.Text(x.Description)),
                EntertainmentPresentation.Text(x.Address), x.Images.FirstOrDefault(), city?.Id, city?.Name ?? x.NativeCityName,
                city?.ProvinceId, city?.ProvinceName, x.CategoryCode is { } category ? $"entertainment:{category}" : "entertainment",
                EntertainmentPresentation.Text(x.CategoryTitle) ?? "تفریح و سرگرمی", EntertainmentPresentation.Text(x.SupplierTitle)!, x.ObservedAtUtc!.Value,
                x.ObservedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-Provider.CatalogRefreshMinutes));
        }).ToArray();
        await tx.CommitAsync(ct);
        return new(items, scope.Partial ? null : count, scope.Partial);
    }
    public async Task<IReadOnlyList<EntertainmentDiscoveryCity>> CitiesAsync(CancellationToken ct)
    {
        if (!Provider.Enabled) return [];
        var scope = await Scope(null, null, null, null, null, ct);
        var codes = await scope.Rows.Select(x => x.LocationCode).Distinct().ToArrayAsync(ct);
        return codes.OfType<string>().Where(scope.Geo.ContainsKey).Select(x => scope.Geo[x]).DistinctBy(x => x.Id)
            .Select(x => new EntertainmentDiscoveryCity(x.Id, x.Name, x.ProvinceId, x.ProvinceName)).ToArray();
    }
    private async Task<IReadOnlyList<EntertainmentProgramDto>> Profiles(IReadOnlyList<TouristPanelProgram> rows, Dictionary<string, CityDto> geo, CancellationToken ct)
    {
        var nativeIds = rows.Select(x => x.SupplierId).Distinct().ToArray();
        var keys = await db.Suppliers.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey && nativeIds.Contains(x.NativeId)).ToDictionaryAsync(x => x.NativeId, x => x.SupplierKey, ct);
        return rows.Select(row => {
            var city = row.LocationCode is { } code ? geo.GetValueOrDefault(code) : null;
            return new EntertainmentProgramDto(keys.GetValueOrDefault(row.SupplierId).ToString("N"), row.ProgramKey.ToString("N"),
                EntertainmentPresentation.Text(row.Title)!, EntertainmentPresentation.Text(row.Description), row.Images.FirstOrDefault(), [],
                new(city?.ProvinceName ?? "", city?.Name ?? row.NativeCityName ?? "", EntertainmentPresentation.Text(row.Address) ?? "", null), [])
            { SchedulingMode = row.ProgramType == 2 ? "Scheduled" : "Undated", Rules = EntertainmentPresentation.Text(row.Rules), Gallery = row.Images,
                LocationCode = row.LocationCode, ExternalCategoryCode = row.CategoryCode?.ToString(),
                UnavailableReason = !Provider.IsSaleConfigured ? "فروش این خدمت هنوز فعال نشده است" : null };
        }).ToArray();
    }
    public async Task<EntertainmentProgramDto?> ProfileAsync(Guid programKey, CancellationToken ct)
    {
        var scope = await Scope(null, null, null, null, null, ct);
        var row = await scope.Rows.SingleOrDefaultAsync(x => x.ProgramKey == programKey, ct);
        if (row == null)
        {
            if (await db.Programs.AnyAsync(x => x.ProgramKey == programKey && x.AccountKey == Provider.AccountKey && x.IsListed && (x.ObservedAtUtc == null || x.ObservedAtUtc < Cutoff || x.ListedAtUtc < Cutoff), ct)) throw new EntertainmentUnavailableException();
            return null;
        }
        return (await Profiles([row], scope.Geo, ct)).Single();
    }
    public async Task<EntertainmentCatalogPage> ProgramsAsync(string? search, string? supplierKey, int page, int size, string? location, string? category, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var scope = await Scope(search, location, category, null, null, ct);
        var rows = scope.Rows;
        if (!string.IsNullOrWhiteSpace(supplierKey))
        {
            if (!Guid.TryParse(supplierKey, out var key)) return new([], page, size, 0);
            var native = await db.Suppliers.Where(x => x.AccountKey == Provider.AccountKey && x.SupplierKey == key).Select(x => x.NativeId).SingleOrDefaultAsync(ct);
            rows = rows.Where(x => x.SupplierId == native);
        }
        var count = await rows.CountAsync(ct);
        var selected = await Ordered(rows).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = await Profiles(selected, scope.Geo, ct);
        await tx.CommitAsync(ct);
        return new(items, page, size, count) { IsPartial = scope.Partial };
    }
    public async Task<EntertainmentSupplierPage> SuppliersAsync(int page, int size, string? location, string? category, CancellationToken ct)
    {
        var scope = await Scope(null, location, category, null, null, ct);
        var query = db.Suppliers.AsNoTracking().Where(s => s.AccountKey == Provider.AccountKey && scope.Rows.Any(p => p.SupplierId == s.NativeId));
        var count = await query.CountAsync(ct);
        var suppliers = await query.OrderBy(x => x.SupplierKey).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = suppliers.Select(x => x.NativeId).ToArray();
        var titles = await scope.Rows.Where(x => ids.Contains(x.SupplierId)).Select(x => new { x.SupplierId, x.SupplierTitle }).Distinct().ToListAsync(ct);
        return new(suppliers.Select(x => new EntertainmentSupplierDto(x.SupplierKey.ToString("N"),
            EntertainmentPresentation.Text(titles.Where(t => t.SupplierId == x.NativeId).OrderBy(t => t.SupplierTitle, StringComparer.Ordinal).First().SupplierTitle)!, [], null, null, null, new("", "", "", null))).ToArray(), page, size, count) { IsPartial = scope.Partial };
    }
    public async Task<EntertainmentSupplierDto?> SupplierAsync(Guid supplierKey, CancellationToken ct)
    {
        var scope = await Scope(null, null, null, null, null, ct);
        var native = await db.Suppliers.Where(x => x.AccountKey == Provider.AccountKey && x.SupplierKey == supplierKey).Select(x => x.NativeId).SingleOrDefaultAsync(ct);
        if (native == null) return null;
        var title = await scope.Rows.Where(x => x.SupplierId == native).OrderBy(x => x.ProgramKey).Select(x => x.SupplierTitle).FirstOrDefaultAsync(ct);
        return title == null ? null : new(supplierKey.ToString("N"), EntertainmentPresentation.Text(title)!, [], null, null, null, new("", "", "", null));
    }
    public async Task<EntertainmentCatalogFilters> FiltersAsync(CancellationToken ct)
    {
        var scope = await Scope(null, null, null, null, null, ct);
        var locations = await scope.Rows.Where(x => x.LocationCode != null).Select(x => new { x.LocationCode, x.NativeCityName }).Distinct().ToListAsync(ct);
        var categories = await scope.Rows.Where(x => x.CategoryCode != null).Select(x => new { x.CategoryCode, x.CategoryTitle }).Distinct().ToListAsync(ct);
        return new(locations.DistinctBy(x => x.LocationCode).Select(x => new EntertainmentFilterOption(x.LocationCode!, scope.Geo.GetValueOrDefault(x.LocationCode!)?.Name ?? x.NativeCityName ?? "")).OrderBy(x => x.Title).ToArray(),
            categories.DistinctBy(x => x.CategoryCode).Select(x => new EntertainmentFilterOption(x.CategoryCode!.Value.ToString(), EntertainmentPresentation.Text(x.CategoryTitle) ?? "تفریح و سرگرمی")).OrderBy(x => x.Title).ToArray());
    }
    public async Task<EntertainmentProgramIdentity?> ResolveAsync(Guid programKey, CancellationToken ct)
    {
        if (await ProfileAsync(programKey, ct) is null) return null;
        return await db.Programs.AsNoTracking().Where(x => x.ProgramKey == programKey && x.AccountKey == Provider.AccountKey)
            .Select(x => new EntertainmentProgramIdentity("touristpanel", x.NativeId, x.SupplierId)).SingleOrDefaultAsync(ct);
    }
    public async Task<string?> ResolveNativeAsync(string programKey, CancellationToken ct) =>
        Guid.TryParse(programKey, out var id) ? (await ResolveAsync(id, ct))?.NativeId : null;
    public async Task<IReadOnlyList<EntertainmentLocationMappingDto>> MappingsAsync(CancellationToken ct)
    {
        var mappings = await db.LocationMappings.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey).ToDictionaryAsync(x => x.LocationCode, ct);
        return (await db.Programs.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey && x.LocationCode != null)
            .Select(x => new { x.LocationCode, x.NativeCityName }).Distinct().ToListAsync(ct))
            .Select(x => new EntertainmentLocationMappingDto(x.LocationCode!, x.NativeCityName, mappings.GetValueOrDefault(x.LocationCode!)?.CityId)).ToArray();
    }
    public async Task SetMappingAsync(string code, int cityId, Guid actor, CancellationToken ct)
    {
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities;
        var provinceIds = (await sender.Send(new GetProvincesQuery(true), ct)).Provinces.Select(x => x.Id).ToHashSet();
        if (!cities.Any(x => x.Id == cityId && provinceIds.Contains(x.ProvinceId)) || string.IsNullOrWhiteSpace(code) || code.Length > 200)
            throw new EntertainmentDomainException("شهر یا کد موقعیت معتبر نیست", "INVALID_LOCATION");
        if (!await db.Programs.AnyAsync(x => x.AccountKey == Provider.AccountKey && x.LocationCode == code, ct))
            throw new EntertainmentDomainException("کد موقعیت در اطلاعات Provider وجود ندارد", "UNKNOWN_LOCATION");
        var row = await db.LocationMappings.FindAsync([Provider.AccountKey, code], ct);
        if (row is null) db.LocationMappings.Add(TouristPanelLocationMapping.Create(Provider.AccountKey, code, cityId, actor));
        else row.Update(cityId, actor);
        await db.SaveChangesAsync(ct);
    }
    private static string? Summary(string? text) => text is null ? null : text[..Math.Min(200, text.Length)];
}
