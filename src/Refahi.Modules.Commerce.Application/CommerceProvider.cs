using Refahi.Modules.Commerce.Application.Contracts.Abstraction;

namespace Refahi.Modules.Commerce.Application;

public class CommerceProvider : ICommerceProvider
{
    private readonly ICommerceProviderManager _providerManager;

    public CommerceProvider(ICommerceProviderManager providerManager)
    {
        _providerManager = providerManager;
    }

    public string Key => "Aggregator";

    public async Task<IEnumerable<Offer>> GetOffersAsync(CancellationToken cancellationToken)
    {
        List<Offer> result = new List<Offer>();

        foreach (var item in _providerManager.Providers)
        {
            var o = await item.GetOffersAsync(cancellationToken);

            result.AddRange(o);
        }

        return result;
    }

    public async Task<IEnumerable<Product>> GetProductAsync(CancellationToken cancellationToken)
    {
        List<Product> result = new List<Product>();

        foreach (var item in _providerManager.Providers)
        {
            var p = await item.GetProductAsync(cancellationToken);

            result.AddRange(p);
        }

        return result;
    }

    public async Task<IEnumerable<Seller>> GetSellersAsync(CancellationToken cancellationToken)
    {
        List<Seller> result = new List<Seller>();

        foreach (var item in _providerManager.Providers)
        {
            var s = await item.GetSellersAsync(cancellationToken);

            result.AddRange(s);
        }

        return result;
    }
}
