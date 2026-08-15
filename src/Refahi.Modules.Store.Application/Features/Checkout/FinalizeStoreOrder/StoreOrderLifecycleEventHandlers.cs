using MediatR;
using System.Security.Cryptography;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Application.Features.Vouchers;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

namespace Refahi.Modules.Store.Application.Features.Checkout.FinalizeStoreOrder;

public sealed class StoreOrderPaidEventHandler(
    IStoreOrderRepository orders,
    IStoreOrderMutationLock mutationLock,
    IShopRepository shops,
    IVoucherCodeProtector protector,
    IMediator mediator,
    TimeProvider clock
) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    {
        if (
            !e.SourceModule.Equals("Store", StringComparison.OrdinalIgnoreCase)
            || !e.ReferenceType.Equals("StoreOrder", StringComparison.OrdinalIgnoreCase)
        )
            return;
        await using var handle = await mutationLock.AcquireAsync(e.OrderId, ct);
        var storeOrder = await orders.GetByOrderIdAsync(e.OrderId, ct);
        if (storeOrder is null || storeOrder.OrderId != e.OrderId
            || storeOrder.UserId != e.UserId || e.SourceReferenceId != storeOrder.Id)
            throw new StoreDomainException(
                "مالکیت سفارش فروشگاه برای نهایی‌سازی تایید نشد",
                "VOUCHER_ORDER_OWNERSHIP_MISMATCH");
        var shop = await shops.GetByIdAsync(storeOrder.ShopId, ct);
        if (shop is null || shop.SupplierId != storeOrder.SupplierId)
            throw new StoreDomainException("فروشگاه سفارش یافت نشد", "VOUCHER_SHOP_SNAPSHOT_UNAVAILABLE");
        var supplier = await mediator.Send(new GetSupplierByIdQuery(storeOrder.SupplierId), ct);
        var supplierName = supplier?.BrandName ?? supplier?.CompanyName
            ?? string.Join(' ', new[] { supplier?.FirstName, supplier?.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new StoreDomainException("تامین‌کننده سفارش یافت نشد", "VOUCHER_SUPPLIER_SNAPSHOT_UNAVAILABLE");

        var generated = storeOrder.Items
            .Where(x => x.FulfillmentMethod == FulfillmentMethod.Voucher
                && x.VoucherSourceType != VoucherSourceType.Preloaded)
            .ToDictionary(
                x => x.Id,
                x => (IReadOnlyList<GeneratedVoucherMaterial>)Enumerable.Range(1, x.Quantity)
                    .Select(sequence =>
                    {
                        var plaintext = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                        return new GeneratedVoucherMaterial(
                            sequence,
                            VoucherCode.Hash(plaintext),
                            protector.Protect(plaintext));
                    }).ToArray());
        await orders.CommitPaidAsync(e.OrderId, new StoreOrderPaidContext(
            e.OrderNumber, supplierName, shop.Name, clock.GetUtcNow(), generated), ct);
    }
}

public sealed class StoreOrderCancelledEventHandler(
    IStoreOrderRepository orders,
    IStoreOrderMutationLock mutationLock
) : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent e, CancellationToken ct)
    {
        if (
            !e.SourceModule.Equals("Store", StringComparison.OrdinalIgnoreCase)
            || !e.ReferenceType.Equals("StoreOrder", StringComparison.OrdinalIgnoreCase)
        )
            return;
        await using var handle = await mutationLock.AcquireAsync(e.OrderId, ct);
        var storeOrder = await orders.GetByOrderIdAsync(e.OrderId, ct);
        if (
            storeOrder is null
            || storeOrder.Status
                is StoreOrderStatus.Cancelled
                    or StoreOrderStatus.Refunded
                    or StoreOrderStatus.Paid
        )
            return;
        storeOrder.MarkCancelled();
        await orders.UpdateAsync(storeOrder, ct);
    }
}

public sealed class StoreOrderRefundedEventHandler(
    IStoreOrderRepository orders,
    IStoreOrderMutationLock mutationLock
) : INotificationHandler<OrderRefundedIntegrationEvent>
{
    public async Task Handle(OrderRefundedIntegrationEvent e, CancellationToken ct)
    {
        await using var handle = await mutationLock.AcquireAsync(e.OrderId, ct);
        var storeOrder = await orders.GetByOrderIdAsync(e.OrderId, ct);
        if (storeOrder is null || storeOrder.Status == StoreOrderStatus.Refunded)
            return;
        storeOrder.MarkRefunded();
        await orders.UpdateAsync(storeOrder, ct);
    }
}
