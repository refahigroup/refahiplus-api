using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetProducts;

public sealed class GetCommerceProductsQueryHandler(ICommerceProviderFactory providers, ICacheService cache) :
    IRequestHandler<GetCommerceProductsQuery, CommerceCatalogPage>
{
    public async Task<CommerceCatalogPage> Handle(GetCommerceProductsQuery request, CancellationToken ct)
    {
        var query = new CommerceCatalogQuery(
            request.Search, 
            request.ProviderKey, 
            Math.Max(1, request.PageNumber), 
            Math.Clamp(request.PageSize, 1, 100)
        );

        var enabled = string.IsNullOrWhiteSpace(request.ProviderKey) 
            ? providers.GetEnabledProviders() 
            : [providers.GetRequired(request.ProviderKey)];

        var all = new List<CommerceProductDto>();

        foreach (var provider in enabled)
        {
            var key = $"commerce:catalog:{provider.Key}:{query.Search}:{query.PageNumber}:{query.PageSize}";

            var page = await cache.GetAsync<CommerceCatalogPage>(key) 
                ?? await provider.GetProductsAsync(query, ct);

            await cache.SetAsync(key, page, TimeSpan.FromMinutes(2)); 
            
            all.AddRange(page.Items);
        }

        return new(all, query.PageNumber, query.PageSize, all.Count);
    }
}
