using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Application.Services;

public interface IStoreProductPriceResolver
{
    Task<StoreResolvedPrice> ResolveAsync(
        Guid shopId,
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    Task<StoreResolvedPrice> ResolveAsync(
        Guid shopId,
        Product product,
        Guid? variantId,
        CancellationToken cancellationToken = default);
}

