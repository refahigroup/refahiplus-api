using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Domain.Entities;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
namespace Refahi.Modules.Orders.Infrastructure.Repositories;

public sealed class OrderPromotionOperationStore(OrdersDbContext db) : IOrderPromotionOperationStore
{
    public Task<OrderPromotionOperation?> FindAsync(Guid orderId, string key, CancellationToken ct) => db.PromotionOperations.SingleOrDefaultAsync(x => x.OrderId == orderId && x.IdempotencyKey == key, ct);
    public Task<OrderPromotionOperation?> GetAsync(Guid id, CancellationToken ct) => db.PromotionOperations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Guid[]> PendingOrderIdsAsync(CancellationToken ct) => db.PromotionOperations.Where(x => x.ResultJson == null).OrderBy(x => x.CreatedAt).Select(x => x.OrderId).Take(100).ToArrayAsync(ct);
    public void Add(OrderPromotionOperation operation) => db.Add(operation);
}
