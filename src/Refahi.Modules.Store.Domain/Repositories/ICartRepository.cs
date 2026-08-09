using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default);
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Cart> AddItemAsync(
        Guid userId,
        int moduleId,
        Guid shopId,
        Guid productId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long unitPriceMinor,
        CancellationToken ct = default);
    Task<Cart> AddOfferItemAsync(Guid userId, int moduleId, Guid shopId, Guid productId,
        Guid offerId, Guid? variantId, Guid? sessionId, DateOnly? usageDate, int quantity,
        long originalUnitPriceMinor, long finalUnitPriceMinor, CancellationToken ct = default)
        => throw new NotSupportedException("این repository از سبد Offer-based پشتیبانی نمی‌کند");
    Task<Cart> ReplaceItemAsync(
        Guid userId, int moduleId, Guid shopId, Guid productId,
        Guid? variantId, Guid? sessionId, DateOnly? usageDate,
        int quantity, long unitPriceMinor, CancellationToken ct = default)
        => AddItemAsync(userId, moduleId, shopId, productId, variantId, sessionId,
            usageDate, quantity, unitPriceMinor, ct);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task UpdateAsync(Cart cart, CancellationToken ct = default);
    Task DeleteAsync(Cart cart, CancellationToken ct = default);
}
