namespace Refahi.Modules.Commerce.Domain;

public interface ICommerceRepository
{
    Task<CommerceCart?> GetCartAsync(Guid userId, CancellationToken ct = default);
    Task AddCartAsync(CommerceCart cart, CancellationToken ct = default);
    Task DeleteCartAsync(CommerceCart cart, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<CommerceOrder?> GetOrderAsync(Guid id, CancellationToken ct = default);
    Task<CommerceOrder?> GetFreshOrderAsync(Guid id, CancellationToken ct = default);
    Task<CommerceOrder?> GetOrderByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<CommerceOrder?> GetOrderByIdempotencyAsync(Guid userId, string key, CancellationToken ct = default);
    Task<CommerceOrder?> GetOrderByFulfillmentIdAsync(Guid fulfillmentId, CancellationToken ct = default);
    Task<IReadOnlyList<CommerceOrder>> GetPendingFulfillmentAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<CommerceOrder>> GetOperationsAsync(string? status, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<CommerceOrder>> GetOrdersForRecipientRedactionAsync(DateTimeOffset createdBefore, int take, CancellationToken ct = default);
    Task AddOrderAsync(CommerceOrder order, CancellationToken ct = default);
}
