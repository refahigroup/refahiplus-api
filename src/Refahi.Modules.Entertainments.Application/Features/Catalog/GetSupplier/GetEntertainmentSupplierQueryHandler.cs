using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;

public sealed class GetEntertainmentSupplierQueryHandler(
    IEntertainmentProviderFactory providers,
    ICacheService cache) : IRequestHandler<GetEntertainmentSupplierQuery, EntertainmentSupplierDto?>
{
    public async Task<EntertainmentSupplierDto?> Handle(GetEntertainmentSupplierQuery request, CancellationToken ct)
    {
        var providerKey = request.ProviderKey.Trim().ToLowerInvariant();
        var supplierKey = request.SupplierKey.Trim();
        var cacheKey = $"entertainments:supplier:{providerKey}:{supplierKey.ToLowerInvariant()}";
        var cached = await cache.GetAsync<EntertainmentSupplierDto>(cacheKey);
        if (cached is not null)
            return cached;

        var supplier = await providers.GetRequired(providerKey).GetSupplierAsync(supplierKey, ct);
        if (supplier is not null
            && supplier.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase)
            && supplier.SupplierKey.Equals(supplierKey, StringComparison.OrdinalIgnoreCase))
            await cache.SetAsync(cacheKey, supplier, TimeSpan.FromMinutes(2));

        return supplier;
    }
}
