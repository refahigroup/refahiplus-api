using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;

namespace Refahi.Modules.Charge.Tests;

public sealed class DigitalOrderTransitionTests
{
    [Fact]
    public void Digital_order_can_move_from_processing_to_delivered()
    {
        var order = Order.Create(Guid.NewGuid(), "Charge", Guid.NewGuid(), "key", "ChargeRequest",
            [new OrderItemData("شارژ", 50_000, 1, 0, Guid.NewGuid(), "mobile-charge.direct", null, null, DeliveryMethod.None)]);
        order.MarkAsReserved(Guid.NewGuid()); order.MarkAsPaid(Guid.NewGuid());
        order.UpdateStatus(OrderStatus.Processing); order.UpdateStatus(OrderStatus.Delivered);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }
}
