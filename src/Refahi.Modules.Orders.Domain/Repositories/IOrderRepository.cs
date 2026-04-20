using Refahi.Modules.Orders.Domain.Aggregates;

namespace Refahi.Modules.Orders.Domain.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<List<Order>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(int page, int pageSize, string? status, Guid? userId, string? sourceModule, CancellationToken ct = default);
    Task<int> CountAllAsync(string? status, Guid? userId, string? sourceModule, CancellationToken ct = default);
    Task<List<Order>> GetBySourceAsync(string sourceModule, Guid sourceReferenceId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountBySourceAsync(string sourceModule, Guid sourceReferenceId, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
}
