using Refahi.Modules.Orders.Domain.Aggregates;

namespace Refahi.Modules.Orders.Domain.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyWithItemsAsync(
        string idempotencyKey,
        CancellationToken ct = default
    );
    Task<List<Order>> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(
        int page,
        int pageSize,
        string? status,
        Guid? userId,
        string? sourceModule,
        IReadOnlyCollection<Guid>? allowedUserIds = null,
        CancellationToken ct = default
    );
    Task<int> CountAllAsync(
        string? status,
        Guid? userId,
        string? sourceModule,
        IReadOnlyCollection<Guid>? allowedUserIds = null,
        CancellationToken ct = default
    );
    Task<List<Order>> GetBySourceAsync(
        string sourceModule,
        Guid sourceReferenceId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<int> CountBySourceAsync(
        string sourceModule,
        Guid sourceReferenceId,
        CancellationToken ct = default
    );
    Task<(List<Order> Orders, int Total)> GetVendorOrdersAsync(
        IReadOnlyCollection<Guid> supplierIds,
        IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds,
        Guid actorUserId,
        int page,
        int pageSize,
        string? status,
        string? paymentState,
        string? orderNumber,
        IReadOnlyCollection<Guid>? userIds,
        Guid? shopId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default
    ) => Task.FromResult((new List<Order>(), 0));
    Task<Order?> GetVendorOrderByIdAsync(
        Guid orderId,
        IReadOnlyCollection<Guid> supplierIds,
        IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds,
        Guid actorUserId,
        CancellationToken ct = default
    ) => Task.FromResult<Order?>(null);
    Task<(int Pending, int Processing)> GetVendorStatusCountsAsync(
        IReadOnlyCollection<Guid> supplierIds,
        IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds,
        Guid actorUserId,
        CancellationToken ct = default
    ) => Task.FromResult((0, 0));
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
}
