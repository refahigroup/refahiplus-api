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

    [Fact]
    public void Provider_invoice_number_respects_eniac_contract()
    {
        var first = Create();
        var second = Create();

        Assert.Equal(ChargeRequest.ProviderInvoiceNumberMaxLength, first.CustomerInvoiceNumber.Length);
        Assert.Matches("^CHG[0-9a-f]{22}$", first.CustomerInvoiceNumber);
        Assert.NotEqual(first.CustomerInvoiceNumber, second.CustomerInvoiceNumber);
    }

    [Fact]
    public void Legacy_invoice_number_is_normalized_before_first_provider_call()
    {
        var now = DateTime.UtcNow;
        var request = Create(now);
        typeof(ChargeRequest).GetProperty(nameof(ChargeRequest.CustomerInvoiceNumber))!
            .SetValue(request, $"CHG{Guid.NewGuid():N}");
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);

        request.StartProcessing("worker", now, TimeSpan.FromMinutes(5));

        Assert.Equal(ChargeRequest.ProviderInvoiceNumberMaxLength, request.CustomerInvoiceNumber.Length);
        Assert.Matches("^CHG[0-9a-f]{22}$", request.CustomerInvoiceNumber);
    }

    [Fact]
    public void Failed_refund_attempt_preserves_recovery_data_and_releases_lease()
    {
        var now = DateTime.UtcNow;
        var request = Create(now);
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);
        request.BeginRefund("عدم انجام شارژ", "refund-key", now);
        request.StartRefundAttempt("worker-1", now, TimeSpan.FromMinutes(5));

        request.MarkRefundAttemptFailed("temporary failure", now.AddMinutes(2), now.AddSeconds(1));

        Assert.Equal(ChargeRequestStatus.Refunding, request.Status);
        Assert.Equal("refund-key", request.RefundIdempotencyKey);
        Assert.Equal("عدم انجام شارژ", request.RefundReason);
        Assert.Equal(1, request.RefundAttemptCount);
        Assert.Equal("temporary failure", request.RefundLastError);
        Assert.Null(request.ProcessingLeaseUntil);
        Assert.Equal(now.AddMinutes(2), request.NextReconciliationAt);
    }

    [Fact]
    public void Refunding_request_reuses_original_idempotency_data()
    {
        var now = DateTime.UtcNow;
        var request = Create(now);
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);
        request.BeginRefund("original reason", "original-key", now);

        request.BeginRefund("different reason", "different-key", now.AddMinutes(1));

        Assert.Equal("original-key", request.RefundIdempotencyKey);
        Assert.Equal("original reason", request.RefundReason);
    }

    [Fact]
    public void Fulfilled_request_cannot_be_refunded_as_a_failed_fulfillment()
    {
        var now = DateTime.UtcNow;
        var request = Create(now);
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);
        request.MarkFulfilled("rrn", "trace", 0, "0", "ok", now);

        Assert.Throws<InvalidOperationException>(() =>
            request.BeginRefund("invalid refund", "refund-key", now));
    }

    private static ChargeRequest Create(DateTime? now = null, DateTime? expires = null)
    {
        var created = now ?? DateTime.UtcNow;
        return ChargeRequest.Create(Guid.NewGuid(), "Eniac", ChargeOperator.Irancell, ChargeServiceType.DirectCharge,
            "09350000000", null, "CUSTOM", "شارژ مستقیم", 1001, 0, null, 1, "{}", 50_000,
            null, 0, 0, 0, 50_000, Guid.NewGuid().ToString("N"), created, expires ?? created.AddMinutes(20));
    }
}
