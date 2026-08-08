using Refahi.Modules.Orders.Domain.Aggregates;

namespace Refahi.Modules.Orders.Tests;

public sealed class OrderPaymentEligibilityTests
{
    [Fact]
    public void Pending_unpaid_order_is_payable()
    {
        var order = CreateOrder();

        var result = order.GetPaymentEligibility(DateTimeOffset.UtcNow);

        Assert.True(result.CanPay);
        Assert.Null(result.UnavailableReason);
    }

    [Fact]
    public void Pending_reserved_order_is_payable()
    {
        var order = CreateOrder();
        order.MarkAsReserved(Guid.NewGuid());

        Assert.True(order.GetPaymentEligibility(DateTimeOffset.UtcNow).CanPay);
    }

    [Fact]
    public void Paid_order_is_not_payable()
    {
        var order = CreateOrder();
        order.MarkAsReserved(Guid.NewGuid());
        order.MarkAsPaid(Guid.NewGuid());

        Assert.False(order.GetPaymentEligibility(DateTimeOffset.UtcNow).CanPay);
    }

    [Fact]
    public void Cancelled_unpaid_order_is_not_payable()
    {
        var order = CreateOrder();
        order.Cancel();

        Assert.False(order.GetPaymentEligibility(DateTimeOffset.UtcNow).CanPay);
    }

    [Fact]
    public void Expired_pending_order_is_not_payable()
    {
        var order = CreateOrder(DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = order.GetPaymentEligibility(DateTimeOffset.UtcNow);

        Assert.False(result.CanPay);
        Assert.Equal("مهلت پرداخت سفارش به پایان رسیده است", result.UnavailableReason);
    }

    private static Order CreateOrder(DateTimeOffset? payableUntil = null) => Order.Create(
        Guid.NewGuid(),
        "Store",
        Guid.NewGuid(),
        Guid.NewGuid().ToString("N"),
        "Cart",
        [new OrderItemData("کالا", 100_000, 1, 0, Guid.NewGuid(), "store", null, null)],
        payableUntil: payableUntil);
}
