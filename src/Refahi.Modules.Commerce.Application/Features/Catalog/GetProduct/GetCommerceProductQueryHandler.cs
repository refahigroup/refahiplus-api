using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetProduct;

public sealed class GetCommerceProductQueryHandler(ICommerceProviderFactory providers, ICacheService cache) :
    IRequestHandler<GetCommerceProductQuery, CommerceProductDto?>
{
    public async Task<CommerceProductDto?> Handle(GetCommerceProductQuery request, CancellationToken ct)
    {
        var key = $"commerce:product:{request.ProviderKey}:{request.ProductKey}";

        var cached = await cache.GetAsync<CommerceProductDto>(key); 

        if (cached is not null) 
            return cached;

        var product = await providers.GetRequired(request.ProviderKey).GetProductAsync(request.ProductKey, ct);

        if (product is not null) 
            await cache.SetAsync(key, product, TimeSpan.FromMinutes(2)); 
        
        return product;
    }
}
