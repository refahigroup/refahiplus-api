namespace Refahi.Modules.CreditPayments.Domain;

public enum FundingContinuationStatus : short
{
    Created = 1,
    RedirectReady = 2,
    PendingRecovery = 3,
    Succeeded = 4,
    Failed = 5,
    Canceled = 6
}

public sealed class FundingContinuation
{
    private FundingContinuation() { }

    public Guid Id { get; private set; }
    public Guid GatewayPaymentId { get; private set; }
    public Guid CheckoutSessionId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PersonalWalletId { get; private set; }
    public long RequiredDeficitMinor { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string ClientReference { get; private set; } = string.Empty;
    public Guid? WalletFundingSessionId { get; private set; }
    public FundingContinuationStatus Status { get; private set; }
    public DateTimeOffset AutoResumeConsentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? FundingStartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public long Version { get; private set; }

    public bool IsActive => Status is FundingContinuationStatus.Created
        or FundingContinuationStatus.RedirectReady
        or FundingContinuationStatus.PendingRecovery;

    public static FundingContinuation Create(
        Guid gatewayPaymentId,
        Guid checkoutSessionId,
        Guid customerId,
        Guid personalWalletId,
        long requiredDeficitMinor,
        DateTimeOffset now)
    {
        if (gatewayPaymentId == Guid.Empty || checkoutSessionId == Guid.Empty
            || customerId == Guid.Empty || personalWalletId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه‌های ادامه تأمین مالی نامعتبر است");
        if (requiredDeficitMinor <= 0)
            throw new CreditPaymentsDomainException("کسری تأمین مالی باید مثبت باشد");
        var id = Guid.NewGuid();
        return new FundingContinuation
        {
            Id = id,
            GatewayPaymentId = gatewayPaymentId,
            CheckoutSessionId = checkoutSessionId,
            CustomerId = customerId,
            PersonalWalletId = personalWalletId,
            RequiredDeficitMinor = requiredDeficitMinor,
            Currency = "IRR",
            ClientReference = $"cpg-funding-{id:N}",
            Status = FundingContinuationStatus.Created,
            AutoResumeConsentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            NextRetryAt = now,
            Version = 1
        };
    }

    public void BindSession(Guid sessionId, DateTimeOffset now)
    {
        if (sessionId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه نشست تأمین مالی نامعتبر است");
        if (WalletFundingSessionId is not null && WalletFundingSessionId != sessionId)
            throw new CreditPaymentsDomainException("نشست تأمین مالی قابل تغییر نیست");
        WalletFundingSessionId = sessionId;
        Status = FundingContinuationStatus.RedirectReady;
        FundingStartedAt ??= now;
        NextRetryAt = now.AddSeconds(30);
        Touch(now);
    }

    public void MarkPendingRecovery(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (!IsActive)
            return;
        Status = FundingContinuationStatus.PendingRecovery;
        FundingStartedAt ??= now;
        LastCheckedAt = now;
        NextRetryAt = nextRetryAt;
        RetryCount++;
        ReleaseLease();
        Touch(now);
    }

    public void MarkSucceeded(DateTimeOffset now) => Complete(FundingContinuationStatus.Succeeded, now);
    public void MarkFailed(DateTimeOffset now) => Complete(FundingContinuationStatus.Failed, now);
    public void MarkCanceled(DateTimeOffset now) => Complete(FundingContinuationStatus.Canceled, now);

    public void DisableAutomaticResume(DateTimeOffset now)
    {
        NextRetryAt = Status == FundingContinuationStatus.Succeeded ? null : NextRetryAt;
        ReleaseLease();
        Touch(now);
    }

    public void AcquireLease(string owner, DateTimeOffset until, DateTimeOffset now)
    {
        if ((!IsActive && !(Status == FundingContinuationStatus.Succeeded && NextRetryAt is not null))
            || string.IsNullOrWhiteSpace(owner) || until <= now)
            throw new CreditPaymentsDomainException("اجاره بازیابی تأمین مالی نامعتبر است");
        LeaseOwner = owner;
        LeaseUntil = until;
        Touch(now);
    }

    public void AcquireAutomaticResumeLease(string owner, DateTimeOffset until, DateTimeOffset now)
    {
        if (Status != FundingContinuationStatus.Succeeded
            || string.IsNullOrWhiteSpace(owner) || until <= now)
            throw new CreditPaymentsDomainException("اجاره ادامه خودکار پرداخت نامعتبر است");
        LeaseOwner = owner;
        LeaseUntil = until;
        Touch(now);
    }

    public void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseUntil = null;
    }

    public void SchedulePostFundingEvaluation(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (Status != FundingContinuationStatus.Succeeded)
            throw new CreditPaymentsDomainException("زمان‌بندی ارزیابی پس از شارژ مجاز نیست");
        LastCheckedAt = now;
        NextRetryAt = nextRetryAt;
        RetryCount++;
        ReleaseLease();
        Touch(now);
    }

    public void CompletePostFundingEvaluation(DateTimeOffset now)
    {
        if (Status != FundingContinuationStatus.Succeeded)
            throw new CreditPaymentsDomainException("تکمیل ارزیابی پس از شارژ مجاز نیست");
        LastCheckedAt = now;
        NextRetryAt = null;
        ReleaseLease();
        Touch(now);
    }

    private void Complete(FundingContinuationStatus status, DateTimeOffset now)
    {
        if (Status is FundingContinuationStatus.Succeeded
            or FundingContinuationStatus.Failed
            or FundingContinuationStatus.Canceled)
            return;
        Status = status;
        CompletedAt = now;
        LastCheckedAt = now;
        NextRetryAt = null;
        ReleaseLease();
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }
}
