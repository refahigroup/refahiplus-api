using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;
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
            request.SellerKey,
            Math.Max(1, request.PageNumber), 
            Math.Clamp(request.PageSize, 1, 100)
        );

        var enabled = string.IsNullOrWhiteSpace(request.ProviderKey) 
            ? providers.GetEnabledProviders() 
            : [providers.GetRequired(request.ProviderKey)];

        var all = new List<CommerceProductDto>();

        foreach (var provider in enabled)
        {
            var key = $"commerce:catalog:all:{provider.Key}:{query.Search}:{query.SellerKey}";
            var items = await cache.GetAsync<IReadOnlyList<CommerceProductDto>>(key);
            if (items is null)
            {
                var collected = new List<CommerceProductDto>();
                var providerPageNumber = 1;
                while (true)
                {
                    var providerPage = await provider.GetProductsAsync(
                        query with { ProviderKey = provider.Key, PageNumber = providerPageNumber, PageSize = 100 }, ct);
                    collected.AddRange(providerPage.Items);
                    if (collected.Count >= providerPage.TotalCount || providerPage.Items.Count == 0)
                        break;
                    providerPageNumber++;
                }

                items = collected;
                await cache.SetAsync(key, items, TimeSpan.FromMinutes(2));
            }

            all.AddRange(items);
        }

        var ordered = all
            .Where(item => string.IsNullOrWhiteSpace(query.SellerKey)
                           || item.SellerKey.Equals(query.SellerKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => (item.ProviderKey, item.ProductKey))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProductKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pageItems = ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return new(pageItems, query.PageNumber, query.PageSize, ordered.Length);
    }
}
