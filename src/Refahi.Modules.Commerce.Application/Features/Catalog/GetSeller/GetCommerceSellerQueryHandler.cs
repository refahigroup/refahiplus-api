using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSeller;

public sealed class GetCommerceSellerQueryHandler(
    ICommerceProviderFactory providers,
    ICacheService cache) : IRequestHandler<GetCommerceSellerQuery, CommerceSellerDto?>
{
    public async Task<CommerceSellerDto?> Handle(GetCommerceSellerQuery request, CancellationToken ct)
    {
        var providerKey = request.ProviderKey.Trim().ToLowerInvariant();
        var sellerKey = request.SellerKey.Trim();
        var cacheKey = $"commerce:seller:{providerKey}:{sellerKey.ToLowerInvariant()}";
        var cached = await cache.GetAsync<CommerceSellerDto>(cacheKey);
        if (cached is not null)
            return cached;

        var seller = await providers.GetRequired(providerKey).GetSellerAsync(sellerKey, ct);
        if (seller is not null
            && seller.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase)
            && seller.SellerKey.Equals(sellerKey, StringComparison.OrdinalIgnoreCase))
            await cache.SetAsync(cacheKey, seller, TimeSpan.FromMinutes(2));

        return seller;
    }
}
