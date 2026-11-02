using Refahi.Modules.Orders.Domain.Entities;
namespace Refahi.Modules.Orders.Domain.Repositories;

public interface IOrderPromotionOperationStore
{
    Task<OrderPromotionOperation?> FindAsync(Guid orderId, string key, CancellationToken ct);
    Task<OrderPromotionOperation?> GetAsync(Guid id, CancellationToken ct);
    Task<Guid[]> PendingOrderIdsAsync(CancellationToken ct);
    void Add(OrderPromotionOperation operation);
}
