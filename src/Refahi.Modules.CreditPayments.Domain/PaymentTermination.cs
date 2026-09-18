namespace Refahi.Modules.CreditPayments.Domain;

public enum PaymentTerminationKind : short { Cancel = 1, Expire = 2 }
public enum PaymentTerminationSource : short { Merchant = 1, Customer = 2, System = 3 }
public enum PaymentTerminationStatus : short
{
    Requested = 1,
    PendingRecovery = 2,
    Completed = 3,
    CaptureWon = 4
}

public sealed class PaymentTermination
{
    private PaymentTermination() { }

    public Guid Id { get; private set; }
    public Guid GatewayPaymentId { get; private set; }
    public PaymentTerminationKind Kind { get; private set; }
    public PaymentTerminationSource Source { get; private set; }
    public PaymentTerminationStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public string ReleaseReference { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public bool IsUnresolved => Status is PaymentTerminationStatus.Requested or PaymentTerminationStatus.PendingRecovery;

    public static PaymentTermination Create(Guid paymentId, PaymentTerminationKind kind,
        PaymentTerminationSource source, string? reason, DateTimeOffset now)
    {
        if (paymentId == Guid.Empty) throw new CreditPaymentsDomainException("شناسه پرداخت نامعتبر است");
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason?.Length > 250)
            throw new CreditPaymentsDomainException("دلیل لغو حداکثر ۲۵۰ نویسه است");
        return new PaymentTermination
        {
            Id = Guid.NewGuid(), GatewayPaymentId = paymentId, Kind = kind, Source = source,
            Status = PaymentTerminationStatus.Requested, Reason = normalizedReason,
            ReleaseReference = $"cpg-release-{paymentId:N}", RequestedAt = now,
            CreatedAt = now, UpdatedAt = now, Version = 1
        };
    }

    public void MarkPendingRecovery(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (!IsUnresolved) return;
        Status = PaymentTerminationStatus.PendingRecovery;
        LastCheckedAt = now; NextRetryAt = nextRetryAt; RetryCount++;
        UpdatedAt = now; Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == PaymentTerminationStatus.Completed) return;
        if (!IsUnresolved) throw new CreditPaymentsDomainException("تکمیل خاتمه پرداخت مجاز نیست");
        Status = PaymentTerminationStatus.Completed;
        ResolvedAt = now; LastCheckedAt = now; NextRetryAt = null; UpdatedAt = now; Version++;
    }

    public void MarkCaptureWon(DateTimeOffset now)
    {
        if (Status == PaymentTerminationStatus.CaptureWon) return;
        if (!IsUnresolved) throw new CreditPaymentsDomainException("ثبت غلبه پرداخت مجاز نیست");
        Status = PaymentTerminationStatus.CaptureWon;
        ResolvedAt = now; LastCheckedAt = now; NextRetryAt = null; UpdatedAt = now; Version++;
    }
}
