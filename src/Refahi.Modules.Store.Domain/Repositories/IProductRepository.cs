using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Product?> GetDisplayableBySlugAsync(
        string slug,
        IReadOnlyList<Guid> allowedAgreementProductIds,
        CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetPagedAsync(
        Guid? shopId, int page, int pageSize, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetPagedAdminAsync(
        Guid? shopId, bool? isDeleted,
        int page, int pageSize, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> SearchAsync(
        string query, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Full-text search scoped to products whose AgreementProductId ∈ allowedAgreementProductIds.</summary>
    Task<(List<Product> Items, int Total)> SearchAsync(
        string query, IReadOnlyList<Guid> allowedAgreementProductIds,
        int page, int pageSize, CancellationToken ct = default);
    /// <summary>Batch-fetches products by IDs including Images. Does not filter IsDeleted/IsAvailable.</summary>
    Task<List<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    /// <summary>Batch-fetches products by IDs including variants and related variant metadata. Does not filter IsDeleted/IsAvailable.</summary>
    Task<List<Product>> GetByIdsForAdminWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task AddVariantAttributeAsync(Product product, VariantAttribute attribute, CancellationToken ct = default);
    Task AddVariantAttributeValueAsync(Product product, VariantAttributeValue value, CancellationToken ct = default);
    Task AddProductVariantAsync(Product product, ProductVariant variant, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
