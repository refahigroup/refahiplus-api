using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default);
    Task<(List<Shop> Items, int Total)> GetPagedAsync(
        ShopType? shopType, ShopStatus? status, int page, int size, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default);
    Task AddAsync(Shop shop, CancellationToken ct = default);
    Task UpdateAsync(Shop shop, CancellationToken ct = default);
}
