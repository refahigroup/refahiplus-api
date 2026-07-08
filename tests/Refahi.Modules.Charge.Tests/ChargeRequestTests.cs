using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeRequestTests
{
    [Fact]
    public void Processing_before_order_payment_is_rejected()
    {
        var request = Create();
        Assert.Throws<InvalidOperationException>(() => request.StartProcessing("test", DateTime.UtcNow, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Paid_request_can_be_fulfilled_idempotently()
    {
        var now = DateTime.UtcNow; var request = Create(now); var orderId = Guid.NewGuid(); var paymentId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now); request.MarkPaid(orderId, paymentId, now);
        request.StartProcessing("test", now, TimeSpan.FromMinutes(1));
        request.MarkFulfilled("7366609154390368322", "trace", 0, "0", "ok", now);
        request.MarkFulfilled("7366609154390368322", "trace", 0, "0", "ok", now);
        Assert.Equal(ChargeRequestStatus.Fulfilled, request.Status);
        Assert.Equal(paymentId, request.PaymentId);
    }

    [Fact]
    public void Expired_request_cannot_be_converted_to_order()
    {
        var now = DateTime.UtcNow; var request = Create(now.AddMinutes(-30), now.AddMinutes(-10));
        Assert.Throws<InvalidOperationException>(() => request.ConvertToOrder(Guid.NewGuid(), now));
        Assert.Equal(ChargeRequestStatus.Expired, request.Status);
    }

    private static ChargeRequest Create(DateTime? now = null, DateTime? expires = null)
    {
        var created = now ?? DateTime.UtcNow;
        return ChargeRequest.Create(Guid.NewGuid(), "Eniac", ChargeOperator.Irancell, ChargeServiceType.DirectCharge,
            "09350000000", null, "CUSTOM", "شارژ مستقیم", 1001, 0, null, 1, "{}", 50_000,
            null, 0, 0, 0, 50_000, Guid.NewGuid().ToString("N"), created, expires ?? created.AddMinutes(20));
    }
}
