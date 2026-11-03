using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolTicketCatalogProjection(PoolsDbContext db, ISender sender, IPoolProviderFactory providers,
    IExternalMediaService media, TimeProvider clock, ILogger<PoolTicketCatalogProjection> logger) : IPoolCatalogProjection
{
    private List<ProvinceDto>? provinces;
    private List<CityDto>? cities;
    public async Task VerifyIdentityAsync(Guid venueId, string name, string state, string city, PoolSummary summary, CancellationToken ct)
    {
        if (summary.ProviderKey != "poolticket" || await db.PoolTicketContents.AnyAsync(x => x.VenueId == venueId, ct)) return;
        if (Normalize(name) != Normalize(summary.Name) || ProvinceName(state) != ProvinceName(summary.State) || Normalize(city) != Normalize(summary.City))
            throw new PoolException("هویت مجموعه سازمانی با سابقه مطابقت ندارد و نیازمند بررسی است", 502, "POOLS_PROVIDER_IDENTITY_AMBIGUOUS");
    }
    public async Task RefreshAsync(Guid venueId, PoolSummary summary, CancellationToken ct)
    {
        if (summary.ProviderKey != "poolticket") return;
        if (summary.NativeCityId == null || summary.NativeStateId == null)
            throw new PoolException("شناسه جغرافیایی تأمین‌کننده موجود نیست", 502);
        provinces ??= (await sender.Send(new GetProvincesQuery(), ct)).Provinces;
        cities ??= (await sender.Send(new GetCitiesQuery(), ct)).Cities;
        var province = Unique(provinces.Where(x => ProvinceName(x.Name) == ProvinceName(summary.State)));
        var city = province == null ? null : Unique(cities.Where(x => x.ProvinceId == province.Id && Normalize(x.Name) == Normalize(summary.City)));
        var geo = await db.PoolTicketGeographies.FindAsync([summary.NativeCityId], ct);
        if (geo == null) { geo = PoolTicketGeography.Create(summary.NativeCityId); db.Add(geo); }
        geo.Map(summary.NativeStateId, summary.State, summary.City, province?.Id, city?.Id, clock.GetUtcNow());
        if (city == null) logger.LogWarning("PoolTicket geography unmapped. NativeStateId={StateId} NativeCityId={CityId}", summary.NativeStateId, summary.NativeCityId);
        var details = await ((IPoolOrganizationProvider)providers.Get("poolticket")).DetailsAsync(summary.Id, ct);
        var content = await db.PoolTicketContents.FindAsync([venueId], ct);
        if (content == null) { content = PoolTicketContent.Create(venueId); db.Add(content); }
        content.Refresh(summary.NativeStateId, summary.NativeCityId, details.DescriptionHtml, details.Address, details.Telephone,
            (double?)summary.Latitude, (double?)summary.Longitude, clock.GetUtcNow(),
            System.Text.Json.JsonSerializer.Serialize(await media.GetPublicUrlsAsync(details.Images, new("Pools", "poolticket"), ct)),
            System.Text.Json.JsonSerializer.Serialize(details.Attributes));
        var venue = await db.Venues.SingleAsync(x => x.Id == venueId, ct);
        venue.RefreshTerms(details.TermsHtml);
        if (summary.ImageUrl != null) await media.GetPublicUrlAsync(summary.ImageUrl, new("Pools", "poolticket"), ct);
        await db.SaveChangesAsync(ct);
    }
    public async Task CompleteAsync(string provider, CancellationToken ct)
    {
        if (provider != "poolticket") return;
        provinces ??= (await sender.Send(new GetProvincesQuery(), ct)).Provinces;
        cities ??= (await sender.Send(new GetCitiesQuery(), ct)).Cities;
        var providerClient = providers.Get(provider);
        var observedStates = new HashSet<string>(); var observedCities = new HashSet<string>();
        foreach (var nativeState in await providerClient.StatesAsync(false, ct))
        {
            if (!observedStates.Add(nativeState.Id)) throw new PoolException("شناسه استان تأمین‌کننده تکراری است", 502);
            var province = Unique(provinces.Where(x => ProvinceName(x.Name) == ProvinceName(nativeState.Name)));
            var provinceMapping = await db.PoolTicketProvinces.FindAsync([nativeState.Id], ct);
            if (provinceMapping == null) { provinceMapping = PoolTicketProvince.Create(nativeState.Id); db.Add(provinceMapping); }
            provinceMapping.Map(nativeState.Name, province?.Id, clock.GetUtcNow());
            foreach (var nativeCity in await providerClient.CitiesAsync(nativeState.Id, false, ct))
            {
                if (!observedCities.Add(nativeCity.Id)) throw new PoolException("شناسه شهر تأمین‌کننده تکراری است", 502);
                var city = province == null ? null : Unique(cities.Where(x => x.ProvinceId == province.Id && Normalize(x.Name) == Normalize(nativeCity.Name)));
                var mapping = await db.PoolTicketGeographies.FindAsync([nativeCity.Id], ct);
                if (mapping == null) { mapping = PoolTicketGeography.Create(nativeCity.Id); db.Add(mapping); }
                mapping.Map(nativeState.Id, nativeState.Name, nativeCity.Name, province?.Id, city?.Id, clock.GetUtcNow());
                if (city == null) logger.LogWarning("PoolTicket geography unmapped. NativeStateId={StateId} NativeCityId={CityId}", nativeState.Id, nativeCity.Id);
            }
        }
        var state = await db.PoolTicketSyncStates.FindAsync(["venues"], ct);
        if (state == null) { state = PoolTicketSyncState.Create(); db.Add(state); }
        state.Complete(clock.GetUtcNow()); await db.SaveChangesAsync(ct);
    }
    // Explicit spelling variants observed in the provider's state/list response.
    private static string ProvinceName(string value) => Normalize(value) switch
    {
        "چهارمحالبختیاری" => "چهارمحالوبختیاری",
        "کهکیلویهوبویراحمد" => "کهگیلویهوبویراحمد",
        var name => name
    };
    internal static string Normalize(string value) => CatalogueText.Normalize(value).Replace(" ", "");
    private static T? Unique<T>(IEnumerable<T> values) where T : class
    { var found = values.Take(2).ToArray(); return found.Length == 1 ? found[0] : null; }
}
