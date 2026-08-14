using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class StoreOrderRepository(StoreDbContext db) : IStoreOrderRepository
{
    public Task<StoreOrder?> GetByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    ) =>
        db
            .StoreOrders.Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
                ct
            );

    public Task<StoreOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        db.StoreOrders.Include(x => x.Items).SingleOrDefaultAsync(x => x.OrderId == orderId, ct);

    public Task<StoreOrder?> GetByIdAsync(Guid storeOrderId, CancellationToken ct = default) =>
        db.StoreOrders.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == storeOrderId, ct);

    public async Task<IReadOnlyList<StoreOrder>> GetByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct = default
    )
    {
        if (orderIds.Count == 0)
            return [];
        return await db
            .StoreOrders.AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrderId.HasValue && orderIds.Contains(x.OrderId.Value))
            .ToListAsync(ct);
    }

    public Task<bool> UserHasPurchasedProductAsync(
        Guid userId,
        Guid productId,
        CancellationToken ct = default
    ) =>
        db.StoreOrders.AsNoTracking().AnyAsync(
            order => order.UserId == userId
                && order.Status == StoreOrderStatus.Paid
                && order.Items.Any(item => item.ProductId == productId),
            ct
        );

    public async Task AddAsync(StoreOrder order, CancellationToken ct = default)
    {
        db.StoreOrders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.Message.Contains(
                    "IX_store_orders_UserId_IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase
                ) == true
            )
        {
            throw new StoreDomainException(
                "کلید یکتایی قبلاً استفاده شده است",
                "IDEMPOTENCY_CONFLICT"
            );
        }
    }

    public async Task UpdateAsync(StoreOrder order, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new StoreConcurrencyException(ex);
        }
    }

    public async Task CommitPaidAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var storeOrder = await db
            .StoreOrders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (storeOrder is null || storeOrder.Status == StoreOrderStatus.Paid)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (storeOrder.Status != StoreOrderStatus.PendingPayment)
            throw new StoreDomainException(
                "وضعیت سفارش فروشگاه برای پرداخت معتبر نیست",
                "INVALID_STORE_ORDER_TRANSITION"
            );

        foreach (var item in storeOrder.Items)
        {
            if (item.ProductSessionId.HasValue)
            {
                var session =
                    await db.ProductSessions.SingleOrDefaultAsync(
                        x => x.Id == item.ProductSessionId,
                        ct
                    ) ?? throw new StoreDomainException("سانس سفارش یافت نشد", "SESSION_NOT_FOUND");
                session.Sell(item.Quantity);
            }
            else if (item.SalesModel == SalesModel.InventoryBased)
            {
                var product =
                    await db
                        .Products.Include(x => x.Variants)
                        .SingleOrDefaultAsync(x => x.Id == item.ProductId, ct)
                    ?? throw new StoreDomainException("محصول سفارش یافت نشد", "PRODUCT_NOT_FOUND");
                if (item.ProductVariantId.HasValue)
                    product.DecreaseVariantStock(item.ProductVariantId.Value, item.Quantity);
                else
                    product.DecreaseStock(item.Quantity);
            }
        }

        var cart = await db
            .Carts.Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.UserId == storeOrder.UserId && x.ModuleId == storeOrder.ModuleId,
                ct
            );
        cart?.Clear();
        storeOrder.MarkPaid();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
