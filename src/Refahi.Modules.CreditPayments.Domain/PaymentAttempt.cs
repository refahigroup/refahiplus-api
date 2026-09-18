namespace Refahi.Modules.CreditPayments.Domain;

public enum PaymentAttemptTrigger : short
{
    CustomerConfirmation = 1,
    FundingAutoResume = 2,
    Recovery = 3
}

public enum PaymentAttemptStatus : short
{
    Created = 1,
    Processing = 2,
    PendingRecovery = 3,
    Succeeded = 4,
    Failed = 5
}

public sealed class PaymentAttempt
{
    private PaymentAttempt() { }

    public Guid Id { get; private set; }
    public Guid GatewayPaymentId { get; private set; }
    public int AttemptSequence { get; private set; }
    public PaymentAttemptTrigger Trigger { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public string? NormalizedOutcomeCode { get; private set; }
    public Guid OrderId { get; private set; }
    public string PolicyId { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public string PolicyHash { get; private set; } = string.Empty;
    public Guid DestinationWalletId { get; private set; }
    public Guid? WalletIntentId { get; private set; }
    public Guid? WalletPaymentId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static PaymentAttempt Create(
        Guid paymentId,
        int sequence,
        PaymentAttemptTrigger trigger,
        Guid orderId,
        string policyId,
        string policyVersion,
        string policyHash,
        Guid destinationWalletId,
        DateTimeOffset now)
    {
        if (paymentId == Guid.Empty || orderId == Guid.Empty || destinationWalletId == Guid.Empty || sequence <= 0)
            throw new CreditPaymentsDomainException("تلاش پرداخت نامعتبر است");
        return new PaymentAttempt
        {
            Id = Guid.NewGuid(), GatewayPaymentId = paymentId, AttemptSequence = sequence,
            Trigger = trigger, Status = PaymentAttemptStatus.Processing, OrderId = orderId,
            PolicyId = CreditPaymentsText.Required(policyId, 100, "شناسه سیاست مالی نامعتبر است"),
            PolicyVersion = CreditPaymentsText.Required(policyVersion, 100, "نسخه سیاست مالی نامعتبر است"),
            PolicyHash = CreditPaymentsText.Required(policyHash, 64, "هش سیاست مالی نامعتبر است"),
            DestinationWalletId = destinationWalletId,
            StartedAt = now, CreatedAt = now, UpdatedAt = now, Version = 1
        };
    }

    public void MarkPendingRecovery(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (Status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed) return;
        Status = PaymentAttemptStatus.PendingRecovery;
        NormalizedOutcomeCode = "unknown";
        LastCheckedAt = now;
        NextRetryAt = nextRetryAt;
        RetryCount++;
        LeaseOwner = null;
        LeaseUntil = null;
        UpdatedAt = now;
        Version++;
    }

    public void MarkSucceeded(Guid? intentId, Guid paymentId, DateTimeOffset now)
    {
        if (Status == PaymentAttemptStatus.Succeeded) return;
        if (Status == PaymentAttemptStatus.Failed || paymentId == Guid.Empty)
            throw new CreditPaymentsDomainException("توفیق تلاش پرداخت مجاز نیست");
        Status = PaymentAttemptStatus.Succeeded;
        NormalizedOutcomeCode = "captured";
        WalletIntentId = intentId;
        WalletPaymentId = paymentId;
        CompletedAt = now;
        LastCheckedAt = now;
        NextRetryAt = null;
        LeaseOwner = null;
        LeaseUntil = null;
        UpdatedAt = now;
        Version++;
    }

    public void MarkFailed(string outcomeCode, DateTimeOffset now)
    {
        if (Status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed) return;
        Status = PaymentAttemptStatus.Failed;
        NormalizedOutcomeCode = CreditPaymentsText.Required(outcomeCode, 100, "نتیجه پرداخت نامعتبر است");
        CompletedAt = now;
        LastCheckedAt = now;
        NextRetryAt = null;
        LeaseOwner = null;
        LeaseUntil = null;
        UpdatedAt = now;
        Version++;
    }

    public void AcquireLease(string owner, DateTimeOffset until, DateTimeOffset now)
    {
        LeaseOwner = CreditPaymentsText.Required(owner, 100, "مالک lease نامعتبر است");
        LeaseUntil = until;
        LastCheckedAt = now;
        UpdatedAt = now;
        Version++;
    }
}
