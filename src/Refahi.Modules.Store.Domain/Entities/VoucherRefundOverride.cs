using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

/// <summary>
/// Immutable authorization record for the exceptional refund of an order whose voucher was redeemed.
/// Outcome changes are recorded separately as append-only attempts.
/// </summary>
public sealed class VoucherRefundOverride
{
    private VoucherRefundOverride() { }

    public Guid Id { get; private set; }
    public Guid StoreOrderId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string VoucherSnapshotJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string Outcome { get; private set; } = "Pending";

    public static VoucherRefundOverride Create(
        Guid storeOrderId, Guid orderId, Guid adminUserId, string reason,
        string voucherSnapshotJson, string idempotencyKey, string requestFingerprint,
        Guid correlationId, DateTimeOffset createdAtUtc)
    {
        var normalizedReason = reason?.Trim();
        if (storeOrderId == Guid.Empty || orderId == Guid.Empty || adminUserId == Guid.Empty ||
            correlationId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedReason) ||
            normalizedReason.Length > 500 || string.IsNullOrWhiteSpace(voucherSnapshotJson) ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128 ||
            string.IsNullOrWhiteSpace(requestFingerprint))
            throw new StoreDomainException("اطلاعات مجوز استثنای بازگشت وجه معتبر نیست", "INVALID_VOUCHER_REFUND_OVERRIDE");

        return new VoucherRefundOverride
        {
            Id = Guid.NewGuid(), StoreOrderId = storeOrderId, OrderId = orderId,
            AdminUserId = adminUserId, Reason = normalizedReason,
            VoucherSnapshotJson = voucherSnapshotJson, CreatedAtUtc = createdAtUtc,
            IdempotencyKey = idempotencyKey.Trim(), RequestFingerprint = requestFingerprint,
            CorrelationId = correlationId, Outcome = "Pending"
        };
    }
}

public sealed class VoucherRefundOverrideAttempt
{
    private VoucherRefundOverrideAttempt() { }

    public Guid Id { get; private set; }
    public Guid VoucherRefundOverrideId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? PaymentAction { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VoucherRefundOverrideAttempt Create(
        Guid overrideId, int sequenceNumber, string outcome, string? paymentAction,
        string? failureCode, string? failureMessage, DateTimeOffset createdAtUtc)
    {
        if (overrideId == Guid.Empty || sequenceNumber <= 0 ||
            outcome is not ("RefundCompleted" or "ReconciliationRequired"))
            throw new StoreDomainException("نتیجه تلاش بازگشت وجه معتبر نیست", "INVALID_VOUCHER_REFUND_ATTEMPT");

        return new VoucherRefundOverrideAttempt
        {
            Id = Guid.NewGuid(), VoucherRefundOverrideId = overrideId,
            SequenceNumber = sequenceNumber, Outcome = outcome,
            PaymentAction = paymentAction?.Trim(), FailureCode = failureCode?.Trim(),
            FailureMessage = failureMessage?.Trim(), CreatedAtUtc = createdAtUtc
        };
    }
}
