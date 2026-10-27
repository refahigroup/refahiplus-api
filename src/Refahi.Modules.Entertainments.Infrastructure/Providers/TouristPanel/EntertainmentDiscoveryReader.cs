using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

public sealed class EntertainmentDiscoveryReader(EntertainmentDbContext db, ISender sender,
    IOptions<TouristPanelOptions> providerOptions, IOptions<EntertainmentsOptions> moduleOptions) : IEntertainmentDiscovery
{
    private TouristPanelOptions Provider => providerOptions.Value;
    private DateTimeOffset Cutoff => DateTimeOffset.UtcNow.AddDays(-moduleOptions.Value.MaxDiscoveryAgeDays);
    private IQueryable<TouristPanelProgram> Published => db.Programs.AsNoTracking().Where(x =>
        x.AccountKey == Provider.AccountKey && x.IsListed && x.ObservedAtUtc >= Cutoff && x.ListedAtUtc >= Cutoff);

    private async Task<Dictionary<string, CityDto>> Geography(CancellationToken ct)
    {
        var provinces = (await sender.Send(new GetProvincesQuery(true), ct)).Provinces.Select(x => x.Id).ToHashSet();
        var cities = (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.Where(x => provinces.Contains(x.ProvinceId)).ToDictionary(x => x.Id);
        var mappings = await db.LocationMappings.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey).ToListAsync(ct);
        return mappings.Where(x => cities.ContainsKey(x.CityId)).ToDictionary(x => x.LocationCode, x => cities[x.CityId]);
    }
    public async Task<EntertainmentDiscoveryPage> SearchAsync(EntertainmentDiscoverySearch query, int take, CancellationToken ct)
    {
        if (!Provider.Enabled) throw new EntertainmentUnavailableException();
        var geography = await Geography(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var state = await db.SyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.AccountKey == Provider.AccountKey, ct);
        if (state?.LastSuccessUtc is null || state.LastSuccessUtc < Cutoff) throw new EntertainmentUnavailableException();
        var mappedCodes = await db.LocationMappings.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey).Select(x => x.LocationCode).ToListAsync(ct);
        var inactiveCodes = mappedCodes.Except(geography.Keys).ToArray();
        var matchingCodes = geography.Where(x => (!query.ProvinceId.HasValue || x.Value.ProvinceId == query.ProvinceId) &&
            (!query.CityId.HasValue || x.Value.Id == query.CityId)).Select(x => x.Key).ToArray();
        var scoped = db.Programs.AsNoTracking().Where(x => x.AccountKey == Provider.AccountKey && x.IsListed && !inactiveCodes.Contains(x.LocationCode!));
        if (query.ProvinceId.HasValue || query.CityId.HasValue) scoped = scoped.Where(x => matchingCodes.Contains(x.LocationCode!));
        var pending = await scoped.AnyAsync(x => x.ObservedAtUtc == null || x.ObservedAtUtc < Cutoff || x.ListedAtUtc < Cutoff, ct);
        var rows = await Published.Where(x => !inactiveCodes.Contains(x.LocationCode!)).ToListAsync(ct);
        if (pending && !await scoped.AnyAsync(x => x.ObservedAtUtc >= Cutoff && x.ListedAtUtc >= Cutoff, ct)) throw new EntertainmentUnavailableException();
        var words = CatalogueText.Normalize(query.Q).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = rows.Select(x => (Row: x, City: x.LocationCode is { } code ? geography.GetValueOrDefault(code) : null))
            .Where(x => (!query.ProvinceId.HasValue || x.City?.ProvinceId == query.ProvinceId) &&
                (!query.CityId.HasValue || x.City?.Id == query.CityId))
            .Where(x => words.All(w => CatalogueText.Normalize($"{x.Row.Title} {x.Row.Description} {x.Row.Address} {x.Row.CategoryTitle} {x.Row.SupplierTitle} {x.Row.NativeCityName} {x.City?.Name} {x.City?.ProvinceName} توریست پنل").Contains(w, StringComparison.Ordinal)))
            .OrderBy(x => CatalogueText.Normalize(x.Row.Title), CatalogueText.Comparer)
            .ThenBy(x => x.Row.ProgramKey.ToString("N"), StringComparer.Ordinal).ToArray();
        var items = filtered.Take(take).Select(x => new EntertainmentDiscoveryItem(x.Row.ProgramKey, x.Row.Title,
            Summary(x.Row.Description), x.Row.Address, x.Row.Images.FirstOrDefault(), x.City?.Id,
            x.City?.Name ?? x.Row.NativeCityName, x.City?.ProvinceId, x.City?.ProvinceName,
            x.Row.CategoryCode is { } category ? $"touristpanel:{category}" : "entertainment",
            x.Row.CategoryTitle ?? "تفریح و سرگرمی", x.Row.SupplierTitle, x.Row.ObservedAtUtc!.Value,
            x.Row.ObservedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-Provider.CatalogRefreshMinutes))).ToArray();
        await tx.CommitAsync(ct);
        var partial = pending || state.Status != "Ready";
        return new(items, partial ? null : filtered.Length, partial);
    }
    public async Task<IReadOnlyList<EntertainmentDiscoveryCity>> CitiesAsync(CancellationToken ct)
    {
        if (!Provider.Enabled) return [];
        var geo = await Geography(ct);
        var codes = await Published.Select(x => x.LocationCode).Distinct().ToListAsync(ct);
        return codes.OfType<string>().Where(geo.ContainsKey).Select(x => geo[x]).DistinctBy(x => x.Id)
            .Select(x => new EntertainmentDiscoveryCity(x.Id, x.Name, x.ProvinceId, x.ProvinceName)).ToArray();
    }
    public async Task<EntertainmentProgramDto?> ProfileAsync(Guid programKey, CancellationToken ct)
    {
        if (!Provider.Enabled) return null;
        var row = await db.Programs.AsNoTracking().SingleOrDefaultAsync(x => x.ProgramKey == programKey && x.AccountKey == Provider.AccountKey && x.IsListed, ct);
        if (row is null) return null;
        if (row.ObservedAtUtc is null || row.ObservedAtUtc < Cutoff || row.ListedAtUtc < Cutoff) throw new EntertainmentUnavailableException();
        var geo = await Geography(ct);
        var city = row.LocationCode is { } code ? geo.GetValueOrDefault(code) : null;
        if (city is null && row.LocationCode is not null && await db.LocationMappings.AnyAsync(x => x.AccountKey == Provider.AccountKey && x.LocationCode == row.LocationCode, ct)) return null;
        return new("touristpanel", row.SupplierId, row.ProgramKey.ToString("N"), row.Title, row.Description,
            row.Images.FirstOrDefault(), [], new(city?.ProvinceName ?? "", city?.Name ?? row.NativeCityName ?? "", row.Address ?? "", null), [])
        { ProgramType = row.ProgramType, Rules = row.Rules, Gallery = row.Images,
            RequiresManifest = row.RequiresManifest, RequiresIdentityNumber = row.RequiresIdentityNumber,
            LocationCode = row.LocationCode, ExternalCategoryCode = row.CategoryCode?.ToString(),
            UnavailableReason = !Provider.IsSaleConfigured ? "فروش این خدمت هنوز فعال نشده است" : null };
    }
    public async Task<string?> ResolveNativeAsync(string key, CancellationToken ct)
    {
        if (!Guid.TryParse(key, out var id) || !Provider.Enabled) return null;
        if (await ProfileAsync(id, ct) is null) return null;
        return await Published.Where(x => x.ProgramKey == id).Select(x => x.NativeId).SingleOrDefaultAsync(ct);
    }
    public async Task<Guid?> ResolveProgramKeyAsync(string nativeId, CancellationToken ct) =>
        await Published.Where(x => x.NativeId == nativeId).Select(x => (Guid?)x.ProgramKey).SingleOrDefaultAsync(ct);
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
