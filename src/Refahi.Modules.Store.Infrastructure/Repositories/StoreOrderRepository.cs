using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
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
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.StoreOrders.Add(order);
        try
        {
            foreach (var item in order.Items.Where(x =>
                x.FulfillmentMethod == FulfillmentMethod.Voucher
                && x.VoucherSourceType == VoucherSourceType.Preloaded))
            {
                if (!item.VoucherSourceId.HasValue || !order.PayableUntilUtc.HasValue)
                    throw new StoreDomainException("اطلاعات رزرو منبع ووچر ناقص است", "VOUCHER_SOURCE_INVALID");

                var sourceId = item.VoucherSourceId.Value;
                var requiredUntil = order.PayableUntilUtc.Value;
                var availableStatus = (short)VoucherSourceCodeStatus.Available;
                var selected = await db.VoucherSourceCodes
                    .FromSqlInterpolated($@"
                        SELECT * FROM store.voucher_source_codes
                        WHERE ""VoucherSourceId"" = {sourceId}
                          AND ""Status"" = {availableStatus}
                          AND (""ExpiresAtUtc"" IS NULL OR ""ExpiresAtUtc"" > {requiredUntil})
                        ORDER BY ""ExpiresAtUtc"" NULLS LAST, ""RegisteredAtUtc"", ""Id""
                        FOR UPDATE SKIP LOCKED
                        LIMIT {item.Quantity}")
                    .ToListAsync(ct);
                if (selected.Count != item.Quantity)
                    throw new StoreDomainException(
                        "تعداد کد آزاد برای این محصول کافی نیست",
                        "VOUCHER_CODES_UNAVAILABLE");

                for (var index = 0; index < selected.Count; index++)
                {
                    selected[index].Reserve();
                    db.VoucherCodeAllocations.Add(VoucherCodeAllocation.Reserve(
                        selected[index].Id,
                        order.Id,
                        item.Id,
                        index + 1,
                        DateTimeOffset.UtcNow,
                        requiredUntil));
                }
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.Message.Contains(
                    "IX_store_orders_UserId_IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase
                ) == true
            )
        {
            await transaction.RollbackAsync(ct);
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
            if (order.Status == StoreOrderStatus.Cancelled)
            {
                var allocations = await db.VoucherCodeAllocations
                    .Where(x => x.StoreOrderId == order.Id
                        && x.Status == VoucherCodeAllocationStatus.Reserved)
                    .ToListAsync(ct);
                if (allocations.Count > 0)
                {
                    var codeIds = allocations.Select(x => x.VoucherSourceCodeId).ToArray();
                    var codes = await db.VoucherSourceCodes
                        .Where(x => codeIds.Contains(x.Id))
                        .ToDictionaryAsync(x => x.Id, ct);
                    var now = DateTimeOffset.UtcNow;
                    foreach (var allocation in allocations)
                    {
                        allocation.Release(now);
                        if (codes.TryGetValue(allocation.VoucherSourceCodeId, out var code))
                            code.Release();
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new StoreConcurrencyException(ex);
        }
    }

    public async Task CommitPaidAsync(
        Guid orderId,
        StoreOrderPaidContext context,
        CancellationToken ct = default)
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
            else if (item.SalesModel == SalesModel.InventoryBased
                && !(item.FulfillmentMethod == FulfillmentMethod.Voucher
                    && item.VoucherSourceType == VoucherSourceType.Preloaded))
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

        var allocations = await db.VoucherCodeAllocations
            .Where(x => x.StoreOrderId == storeOrder.Id)
            .ToListAsync(ct);
        foreach (var item in storeOrder.Items.Where(x => x.FulfillmentMethod == FulfillmentMethod.Voucher))
        {
            for (var sequence = 1; sequence <= item.Quantity; sequence++)
            {
                var voucher = await db.Vouchers.SingleOrDefaultAsync(
                    x => x.StoreOrderItemId == item.Id && x.SequenceNumber == sequence, ct);
                VoucherCodeAllocation? allocation = null;
                VoucherSourceCode? sourceCode = null;
                string hash;
                string ciphertext;
                DateTimeOffset? expiresAt;
                if (item.VoucherSourceType == VoucherSourceType.Preloaded)
                {
                    allocation = allocations.SingleOrDefault(x =>
                        x.StoreOrderItemId == item.Id && x.SequenceNumber == sequence)
                        ?? throw new StoreDomainException("رزرو کد ووچر یافت نشد", "VOUCHER_CODE_RESERVATION_NOT_FOUND");
                    sourceCode = await db.VoucherSourceCodes.SingleAsync(
                        x => x.Id == allocation.VoucherSourceCodeId, ct);
                    hash = sourceCode.CodeHash;
                    ciphertext = sourceCode.CodeCiphertext;
                    expiresAt = sourceCode.ExpiresAtUtc;
                }
                else
                {
                    var material = context.GeneratedMaterials.GetValueOrDefault(item.Id)?
                        .SingleOrDefault(x => x.SequenceNumber == sequence)
                        ?? throw new StoreDomainException("کد تولیدشده ووچر یافت نشد", "VOUCHER_GENERATION_FAILED");
                    hash = material.CodeHash;
                    ciphertext = material.CodeCiphertext;
                    expiresAt = item.VoucherDefaultValidityDays.HasValue
                        ? context.PaidAtUtc.AddDays(item.VoucherDefaultValidityDays.Value)
                        : null;
                }

                if (voucher is null)
                {
                    voucher = Voucher.Issue(
                        storeOrder.Id, item.Id, orderId, context.OrderNumber, sequence,
                        storeOrder.UserId, item.SupplierId, context.SupplierName,
                        item.ShopId, context.ShopName, item.ProductId, item.ProductTitle,
                        hash, ciphertext, context.PaidAtUtc, expiresAt,
                        item.VoucherSourceId, item.VoucherSourceTitle,
                        item.VoucherSourceType ?? VoucherSourceType.Generated,
                        item.VoucherRedemptionMode ?? VoucherRedemptionMode.RefahiValidation,
                        sourceCode?.Id);
                    db.Vouchers.Add(voucher);
                    db.VoucherDeliveries.Add(VoucherDelivery.Queue(voucher.Id, storeOrder.UserId, context.PaidAtUtc));
                }
                if (allocation is not null && sourceCode is not null
                    && allocation.Status == VoucherCodeAllocationStatus.Reserved)
                {
                    sourceCode.Assign();
                    allocation.Assign(voucher.Id, context.PaidAtUtc);
                }
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

    [Obsolete("Atomic finalization requires StoreOrderPaidContext.")]
    public Task CommitPaidAsync(Guid orderId, CancellationToken ct = default) =>
        throw new NotSupportedException("Atomic Store voucher finalization context is required.");
}
