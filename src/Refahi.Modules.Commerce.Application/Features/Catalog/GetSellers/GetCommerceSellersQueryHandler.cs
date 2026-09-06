using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;

public sealed class GetCommerceSellersQueryHandler(ICommerceProviderFactory providers) : IRequestHandler<GetCommerceSellersQuery, CommerceSellerPage>
{
    public async Task<CommerceSellerPage> Handle(GetCommerceSellersQuery request, CancellationToken ct)
    {
        var all = new List<CommerceSellerDto>(); 
        var unavailable = new List<string>();

        var enabledProviders = providers.GetEnabledProviders()
                                        .Where(p => string.IsNullOrWhiteSpace(request.ProviderKey) || p.Key == request.ProviderKey);

        foreach (var provider in enabledProviders)
        {
            try
            {
                var sellers = await provider.GetSellersAsync(ct);

                if (!string.IsNullOrWhiteSpace(request.LocationCode) || !string.IsNullOrWhiteSpace(request.CategoryCode))
                {
                    var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var page = 1; page <= 1000; page++)
                    {
                        var products = await provider.GetProductsAsync(new(ProviderKey: provider.Key, PageNumber: page, PageSize: 100), ct);

                        var list = products.Items.Where(p => 
                            (string.IsNullOrWhiteSpace(request.LocationCode) || 
                            p.LocationCode == request.LocationCode) &&
                            (string.IsNullOrWhiteSpace(request.CategoryCode) || 
                            p.ExternalCategoryCode == request.CategoryCode)
                        );

                        foreach (var p in list) 
                            matched.Add(p.SellerKey);

                        if (page >= products.TotalPages || products.Items.Count == 0) 
                            break;
                    }

                    sellers = sellers.Where(x => matched.Contains(x.SellerKey))
                                     .ToArray();
                }

                all.AddRange(sellers);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) 
            { 
                throw; 
            }
            catch 
            { 
                unavailable.Add(provider.Key); 
            }
        }
        var sorted = all.DistinctBy(x => (x.ProviderKey, x.SellerKey))
                        .OrderBy(x => x.Title).ThenBy(x => x.ProviderKey)
                        .ThenBy(x => x.SellerKey)
                        .ToArray();

        var number = Math.Max(1, request.PageNumber); 
        var size = Math.Clamp(request.PageSize, 1, 100);

        return new(
            sorted.Skip((number - 1) * size).Take(size).ToArray(), 
            number, 
            size, 
            sorted.Length
        ) 
        { 
            UnavailableProviders = unavailable 
        };
    }
}
