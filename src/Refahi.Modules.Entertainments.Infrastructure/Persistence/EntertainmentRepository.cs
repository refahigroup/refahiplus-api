using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class EntertainmentRepository(EntertainmentDbContext db) : IEntertainmentRepository
{
    public Task<EntertainmentCart?> GetCartAsync(Guid userId, CancellationToken ct = default) =>
        db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task AddCartAsync(EntertainmentCart cart, CancellationToken ct = default) => 
        await db.Carts.AddAsync(cart, ct);

    public Task DeleteCartAsync(EntertainmentCart cart, CancellationToken ct = default) 
    { 
        db.Carts.Remove(cart); 
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => 
        db.SaveChangesAsync(ct);

    public Task<EntertainmentPurchase?> GetOrderAsync(Guid id, CancellationToken ct = default) => 
        Query().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<EntertainmentPurchase?> GetFreshOrderAsync(Guid id, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        return Query().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<EntertainmentPurchase?> GetOrderByOrderIdAsync(Guid orderId, CancellationToken ct = default) => 
        Query().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);

    public Task<EntertainmentPurchase?> GetOrderByIdempotencyAsync(Guid userId, string key, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);

    public Task<EntertainmentPurchase?> GetOrderByFulfillmentIdAsync(Guid fulfillmentId, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(x => x.Fulfillments.Any(f => f.Id == fulfillmentId), ct);

    public async Task<IReadOnlyList<EntertainmentPurchase>> GetPendingFulfillmentAsync(int take, CancellationToken ct = default)
    {
        return await Query().Where(x => x.Status == EntertainmentPurchaseStatus.IssuancePending || x.Status == EntertainmentPurchaseStatus.Issuing || x.Status == EntertainmentPurchaseStatus.CompensationPending
                            || x.Status == EntertainmentPurchaseStatus.AwaitingResult && x.Fulfillments.Any(f => f.NextCheckAt <= DateTimeOffset.UtcNow))
                            .OrderBy(x => x.CreatedAt)
                            .Take(take)
                            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EntertainmentPurchase>> GetOperationsAsync(string? status, int skip, int take, CancellationToken ct = default)
    {
        var q = Query();

        if (Enum.TryParse<EntertainmentPurchaseStatus>(status, true, out var parsed)) 
            q = q.Where(x => x.Status == parsed);
        else if (string.IsNullOrWhiteSpace(status)) 
            q = q.Where(x => x.Status == EntertainmentPurchaseStatus.ManualReview || x.Status == EntertainmentPurchaseStatus.CompensationPending);

        return await q.OrderByDescending(x => x.UpdatedAt).Skip(skip)
                      .Take(take)
                      .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EntertainmentPurchase>> GetOrdersForRecipientRedactionAsync(DateTimeOffset createdBefore, int take, CancellationToken ct = default)
    {
        return await Query().Where(x => x.CreatedAt < createdBefore && x.RecipientMobileProtected != "" &&
                                (x.Status == EntertainmentPurchaseStatus.Completed || x.Status == EntertainmentPurchaseStatus.Cancelled || x.Status == EntertainmentPurchaseStatus.Refunded))
                            .OrderBy(x => x.CreatedAt)
                            .Take(take)
                            .ToListAsync(ct);
    }

    public async Task AddOrderAsync(EntertainmentPurchase order, CancellationToken ct = default) => 
        await db.Orders.AddAsync(order, ct);

    public async Task<IReadOnlyList<EntertainmentPurchase>> GetAwaitingPaymentAsync(int take, CancellationToken ct) =>
        await Query().Where(x => x.Status == EntertainmentPurchaseStatus.AwaitingPayment && x.OrderId != null)
            .OrderBy(x => x.UpdatedAt).Take(take).ToListAsync(ct);
    private IQueryable<EntertainmentPurchase> Query() =>
        db.Orders
          .Include(x => x.Items)
          .Include(x => x.Fulfillments)
          .ThenInclude(x => x.Tickets)
          .Include(x => x.Fulfillments)
          .ThenInclude(x => x.Attempts);
}
