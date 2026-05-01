using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IShopProductRepository
{
    Task<ShopProduct?> GetAsync(Guid shopId, Guid productId, CancellationToken ct = default);
    Task<(List<ShopProduct> Items, int Total)> GetByShopAsync(
        Guid shopId, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<(List<ShopProduct> Items, int Total)> GetByProductAsync(
        Guid productId, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Returns distinct ShopIds that have at least one active ShopProduct whose linked Product.AgreementProductId is in <paramref name="agreementProductIds"/>.</summary>
    [Obsolete("Use GetDisplayableShopIdsByAgreementProductIdsAsync which also enforces IsAvailable/IsDeleted on Product.")]
    Task<IReadOnlyList<Guid>> GetActiveShopIdsByAgreementProductIdsAsync(
        IEnumerable<Guid> agreementProductIds, CancellationToken ct = default);
    /// <summary>Returns distinct ShopIds with at least one displayable product (active ShopProduct; IsAvailable, !IsDeleted Product) whose AgreementProductId ∈ apIds.</summary>
    Task<IReadOnlyList<Guid>> GetDisplayableShopIdsByAgreementProductIdsAsync(
        IReadOnlyList<Guid> apIds, CancellationToken ct = default);
    /// <summary>Returns paginated distinct ProductIds (ordered by Product.CreatedAt desc) for active ShopProducts whose Product.AgreementProductId ∈ apIds.</summary>
    Task<(IReadOnlyList<Guid> ProductIds, int Total)> GetDisplayableProductIdsByAgreementProductIdsAsync(
        IReadOnlyList<Guid> apIds, Guid? shopId, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Returns active, non-deleted ShopProducts keyed by ProductId. If shopId is specified, only that shop's records are included.</summary>
    Task<IReadOnlyDictionary<Guid, ShopProduct>> GetForProductsAsync(
        IReadOnlyList<Guid> productIds, Guid? shopId = null, CancellationToken ct = default);
    Task AddAsync(ShopProduct shopProduct, CancellationToken ct = default);
    Task UpdateAsync(ShopProduct shopProduct, CancellationToken ct = default);
}
