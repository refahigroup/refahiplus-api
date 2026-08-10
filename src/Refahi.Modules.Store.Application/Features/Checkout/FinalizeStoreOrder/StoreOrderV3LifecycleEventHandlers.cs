using MediatR;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Checkout.FinalizeStoreOrder;

public sealed class StoreOrderV3PaidEventHandler(
    IStoreOrderRepository orders,
    IStoreOrderMutationLock mutationLock
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
        await orders.CommitPaidAsync(e.OrderId, ct);
    }
}

public sealed class StoreOrderV3CancelledEventHandler(
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

public sealed class StoreOrderV3RefundedEventHandler(
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
