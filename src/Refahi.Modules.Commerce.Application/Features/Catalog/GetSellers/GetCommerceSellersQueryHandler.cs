using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;

public sealed class GetCommerceSellersQueryHandler(ICommerceProviderFactory providers, ICacheService cache) :
    IRequestHandler<GetCommerceSellersQuery, IReadOnlyList<CommerceSellerDto>>
{
    public async Task<IReadOnlyList<CommerceSellerDto>> Handle(GetCommerceSellersQuery request, CancellationToken ct)
    {
        const string key = "commerce:catalog:sellers:v1";

        var cached = await cache.GetAsync<IReadOnlyList<CommerceSellerDto>>(key); 
        
        if (cached is not null) 
            return cached;

        var result = new List<CommerceSellerDto>();

        foreach (var provider in providers.GetEnabledProviders()) 
            result.AddRange(await provider.GetSellersAsync(ct));

        await cache.SetAsync(key, result, TimeSpan.FromMinutes(2)); 
        
        return result;
    }
}
