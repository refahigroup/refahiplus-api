using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Infrastructure.Persistence;

public sealed class CommerceRepository(CommerceDbContext db) : ICommerceRepository
{
    public Task<CommerceCart?> GetCartAsync(Guid userId, CancellationToken ct = default) =>
        db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task AddCartAsync(CommerceCart cart, CancellationToken ct = default) => 
        await db.Carts.AddAsync(cart, ct);

    public Task DeleteCartAsync(CommerceCart cart, CancellationToken ct = default) 
    { 
        db.Carts.Remove(cart); 
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => 
        db.SaveChangesAsync(ct);

    public Task<CommerceOrder?> GetOrderAsync(Guid id, CancellationToken ct = default) => 
        Query().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<CommerceOrder?> GetOrderByOrderIdAsync(Guid orderId, CancellationToken ct = default) => 
        Query().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);

    public Task<CommerceOrder?> GetOrderByIdempotencyAsync(Guid userId, string key, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);

    public Task<CommerceOrder?> GetOrderByFulfillmentIdAsync(Guid fulfillmentId, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(x => x.Fulfillments.Any(f => f.Id == fulfillmentId), ct);

    public async Task<IReadOnlyList<CommerceOrder>> GetPendingFulfillmentAsync(int take, CancellationToken ct = default)
    {
        return await Query().Where(x => x.Status == CommerceOrderStatus.FulfillmentPending || x.Status == CommerceOrderStatus.Fulfilling || x.Status == CommerceOrderStatus.CompensationPending)
                            .OrderBy(x => x.CreatedAt)
                            .Take(take)
                            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommerceOrder>> GetOperationsAsync(string? status, int skip, int take, CancellationToken ct = default)
    {
        var q = Query();

        if (Enum.TryParse<CommerceOrderStatus>(status, true, out var parsed)) 
            q = q.Where(x => x.Status == parsed);
        else if (string.IsNullOrWhiteSpace(status)) 
            q = q.Where(x => x.Status == CommerceOrderStatus.ManualReview || x.Status == CommerceOrderStatus.CompensationPending);

        return await q.OrderByDescending(x => x.UpdatedAt).Skip(skip)
                      .Take(take)
                      .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommerceOrder>> GetOrdersForRecipientRedactionAsync(DateTimeOffset createdBefore, int take, CancellationToken ct = default)
    {
        return await Query().Where(x => x.CreatedAt < createdBefore && x.RecipientMobileProtected != "" &&
                                (x.Status == CommerceOrderStatus.Completed || x.Status == CommerceOrderStatus.Cancelled || x.Status == CommerceOrderStatus.Refunded))
                            .OrderBy(x => x.CreatedAt)
                            .Take(take)
                            .ToListAsync(ct);
    }

    public async Task AddOrderAsync(CommerceOrder order, CancellationToken ct = default) => 
        await db.Orders.AddAsync(order, ct);

    private IQueryable<CommerceOrder> Query() =>
        db.Orders
          .Include(x => x.Items)
          .Include(x => x.Fulfillments)
          .ThenInclude(x => x.Tickets)
          .Include(x => x.Fulfillments)
          .ThenInclude(x => x.Attempts);
}
