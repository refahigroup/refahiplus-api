using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetCatalogPagedAsync(
        Guid? supplierId,
        int? categoryId,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<List<Product>> GetCatalogEligibilityCandidatesAsync(
        Guid? supplierId,
        int? categoryId,
        CancellationToken ct = default
    );
    Task<(List<Product> Items, int Total)> GetCatalogPageByIdsAsync(
        IReadOnlyCollection<Guid> eligibleIds,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task AddVariantAttributeAsync(
        Product product,
        VariantAttribute attribute,
        CancellationToken ct = default
    );
    Task AddVariantAttributeValueAsync(
        Product product,
        VariantAttributeValue value,
        CancellationToken ct = default
    );
    Task AddProductVariantAsync(
        Product product,
        ProductVariant variant,
        CancellationToken ct = default
    );
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
