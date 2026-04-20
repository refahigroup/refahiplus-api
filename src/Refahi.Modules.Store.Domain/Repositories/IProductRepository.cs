using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Product>> GetByShopIdAsync(Guid shopId, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetPagedAsync(
        int? categoryId, Guid? shopId, long? minPrice, long? maxPrice,
        short? salesModel, int page, int pageSize, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> GetPagedAdminAsync(
        int? categoryId, Guid? shopId, bool? isDeleted,
        int page, int pageSize, CancellationToken ct = default);
    Task<(List<Product> Items, int Total)> SearchAsync(
        string query, int page, int pageSize, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
