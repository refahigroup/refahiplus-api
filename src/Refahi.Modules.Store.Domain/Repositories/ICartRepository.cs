using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserAndModuleIdAsync(
        Guid userId,
        int moduleId,
        CancellationToken ct = default
    );
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
        CancellationToken ct = default
    );
    Task<Cart> AddOfferItemAsync(
        Guid userId,
        int moduleId,
        Guid shopId,
        Guid productId,
        Guid offerId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long originalUnitPriceMinor,
        long finalUnitPriceMinor,
        CancellationToken ct = default
    ) => throw new NotSupportedException("این repository از سبد Offer-based پشتیبانی نمی‌کند");
    async Task<Cart> AddOfferItemsAsync(
        Guid userId,
        int moduleId,
        IReadOnlyList<OfferCartItemSpec> items,
        CancellationToken ct = default
    )
    {
        Cart? cart = null;
        foreach (var item in items)
            cart = await AddOfferItemAsync(
                userId,
                moduleId,
                item.ShopId,
                item.ProductId,
                item.OfferId,
                item.VariantId,
                item.SessionId,
                item.UsageDate,
                item.Quantity,
                item.OriginalUnitPriceMinor,
                item.FinalUnitPriceMinor,
                ct
            );
        return cart ?? throw new ArgumentException("لیست آیتم‌های سبد نمی‌تواند خالی باشد", nameof(items));
    }
    Task<Cart> ReplaceItemAsync(
        Guid userId,
        int moduleId,
        Guid shopId,
        Guid productId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long unitPriceMinor,
        CancellationToken ct = default
    ) =>
        AddItemAsync(
            userId,
            moduleId,
            shopId,
            productId,
            variantId,
            sessionId,
            usageDate,
            quantity,
            unitPriceMinor,
            ct
        );
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task UpdateAsync(Cart cart, CancellationToken ct = default);
    Task DeleteAsync(Cart cart, CancellationToken ct = default);
}

public readonly record struct OfferCartItemSpec(
    Guid ShopId,
    Guid ProductId,
    Guid OfferId,
    Guid? VariantId,
    Guid? SessionId,
    DateOnly? UsageDate,
    int Quantity,
    long OriginalUnitPriceMinor,
    long FinalUnitPriceMinor
);
