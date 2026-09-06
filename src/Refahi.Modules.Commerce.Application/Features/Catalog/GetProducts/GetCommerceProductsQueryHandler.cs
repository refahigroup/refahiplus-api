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
        var unavailable = new List<string>();

        foreach (var provider in enabled)
        {
            try
            {
            var key = $"commerce:catalog:all:{provider.Key}:{query.Search}:{query.SellerKey}";
            var items = provider is ICommerceOfferProvider ? null : await cache.GetAsync<IReadOnlyList<CommerceProductDto>>(key);
            if (items is null)
            {
                var collected = new List<CommerceProductDto>();
                var providerPageNumber = 1;
                while (true)
                {
                    if (providerPageNumber > 1000) throw new InvalidOperationException("سقف صفحه‌بندی کاتالوگ");
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
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { unavailable.Add(provider.Key); }
        }

        var ordered = all
            .Where(item => string.IsNullOrWhiteSpace(request.LocationCode) || item.LocationCode == request.LocationCode)
            .Where(item => string.IsNullOrWhiteSpace(request.CategoryCode) || item.ExternalCategoryCode == request.CategoryCode)
            .Where(item => string.IsNullOrWhiteSpace(query.SellerKey)
                           || item.SellerKey.Equals(query.SellerKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => (item.ProviderKey, item.ProductKey))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProductKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pageItems = ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return new(pageItems, query.PageNumber, query.PageSize, ordered.Length) { UnavailableProviders = unavailable };
    }
}
