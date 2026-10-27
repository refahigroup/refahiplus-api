using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Discovery;

public sealed class EntertainmentsCatalogueSource(IEntertainmentDiscovery discovery) : ICatalogueSource
{
    public async Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query, int take, CancellationToken ct)
    {
        var page = await discovery.SearchAsync(new(query.Q, query.ProvinceId, query.CityId), take, ct);
        return new(page.Items.Select(x => new CatalogueItem($"entertainments:{x.ProgramKey:N}", "entertainments", "entertainment",
            x.ProgramKey.ToString("N"), x.Title, x.Summary, x.ImageUrl, x.CityId, x.CityName, x.ProvinceName, ["توریست‌پنل"],
            $"/entertainments/programs/{x.ProgramKey}", x.ObservedAtUtc, x.IsStale, Category: new(x.CategoryCode ?? "entertainment", x.CategoryTitle ?? "تفریح و سرگرمی"),
            Attributes: [new("provider", "ارائه‌دهنده", "توریست‌پنل"), new("supplier", "تأمین‌کننده", x.SupplierTitle)],
            Locations: [new(x.ProgramKey.ToString("N"), x.ProvinceId, x.ProvinceName, x.CityId, x.CityName, x.Title, x.Address)])).ToArray(),
            page.TotalCount, page.IsPartial);
    }
    public async Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct) =>
        (await discovery.CitiesAsync(ct)).Select(x => new CatalogueCity(x.CityId, x.CityName, x.ProvinceName, x.ProvinceId)).ToArray();
}
