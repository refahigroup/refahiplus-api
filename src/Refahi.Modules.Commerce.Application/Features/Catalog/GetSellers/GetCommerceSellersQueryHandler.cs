using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;

public sealed class GetCommerceSellersQueryHandler(ICommerceProviderFactory providers, ICacheService cache) :
    IRequestHandler<GetCommerceSellersQuery, CommerceSellerPage>
{
    public async Task<CommerceSellerPage> Handle(GetCommerceSellersQuery request, CancellationToken ct)
    {
        const string key = "commerce:catalog:sellers:v1";

        var cached = await cache.GetAsync<IReadOnlyList<CommerceSellerDto>>(key);
        if (cached is null)
        {
            var batches = await Task.WhenAll(providers.GetEnabledProviders()
                .Select(provider => provider.GetSellersAsync(ct)));

            cached = batches
                .SelectMany(items => items)
                .Where(item => !string.IsNullOrWhiteSpace(item.ProviderKey)
                               && !string.IsNullOrWhiteSpace(item.SellerKey))
                .DistinctBy(item => (item.ProviderKey.Trim().ToLowerInvariant(), item.SellerKey.Trim().ToLowerInvariant()))
                .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SellerKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await cache.SetAsync(key, cached, TimeSpan.FromMinutes(2));
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = cached.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        return new(items, pageNumber, pageSize, cached.Count);
    }
}
