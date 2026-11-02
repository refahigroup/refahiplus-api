namespace Refahi.Modules.Entertainments.Domain;

public interface IEntertainmentRepository
{
    Task<EntertainmentCart?> GetCartAsync(Guid userId, CancellationToken ct = default);
    Task AddCartAsync(EntertainmentCart cart, CancellationToken ct = default);
    Task DeleteCartAsync(EntertainmentCart cart, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<EntertainmentPurchase?> GetOrderAsync(Guid id, CancellationToken ct = default);
    Task<EntertainmentPurchase?> GetFreshOrderAsync(Guid id, CancellationToken ct = default);
    Task<EntertainmentPurchase?> GetOrderByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<EntertainmentPurchase?> GetOrderByIdempotencyAsync(Guid userId, string key, CancellationToken ct = default);
    Task<EntertainmentPurchase?> GetOrderByFulfillmentIdAsync(Guid fulfillmentId, CancellationToken ct = default);
    Task<IReadOnlyList<EntertainmentPurchase>> GetPendingFulfillmentAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<EntertainmentPurchase>> GetOperationsAsync(string? status, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<EntertainmentPurchase>> GetOrdersForRecipientRedactionAsync(DateTimeOffset createdBefore, int take, CancellationToken ct = default);
    Task<IReadOnlyList<EntertainmentPurchase>> GetAwaitingPaymentAsync(int take, CancellationToken ct);
    Task AddOrderAsync(EntertainmentPurchase order, CancellationToken ct = default);
}
