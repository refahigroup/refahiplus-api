using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

public sealed class HotelCatalogueProviderRegistry : IHotelCatalogueProviderRegistry
{
    private readonly IHotelCatalogueProvider snapp;
    private readonly IHotelCatalogueProvider lamasoo;
    private readonly bool snappSearchEnabled;
    public HotelCatalogueProviderRegistry(SnappTripCatalogueProvider snapp, LamasooHotelProvider lamasoo,
        IOptions<LamasooOptions> options, IMemoryCache cache, IConfiguration configuration)
    {
        this.snapp = new BoundedHotelCatalogueProvider(snapp, cache, configuration.GetValue("Hotels:Providers:SnappTrip:SearchTimeoutSeconds", 8), configuration.GetValue("Hotels:Providers:SnappTrip:PurchaseEnabled", true));
        this.lamasoo = new BoundedHotelCatalogueProvider(lamasoo, cache, options.Value.TimeoutSeconds);
        snappSearchEnabled = configuration.GetValue("Hotels:Providers:SnappTrip:SearchEnabled", true);
    }
    public IReadOnlyList<IHotelCatalogueProvider> Active => new[] { snapp, lamasoo }.Where(x => x.Name == "SnappTrip" ? snappSearchEnabled : x.Capabilities.Search).ToArray();
    public IHotelCatalogueProvider Get(string name) => name switch
    {
        "SnappTrip" => snapp,
        "Lamasoo" => lamasoo,
        _ => throw new InvalidOperationException("تأمین‌کننده هتل معتبر نیست.")
    };
}
