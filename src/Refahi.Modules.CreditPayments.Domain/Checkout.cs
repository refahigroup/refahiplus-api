namespace Refahi.Modules.CreditPayments.Domain;

public sealed class CheckoutStateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public enum CheckoutSessionStatus : short
{
    Created = 1,
    Authenticated = 2,
    AwaitingFunding = 3,
    Processing = 4,
    Completed = 5,
    Canceled = 6,
    Expired = 7
}

public enum CheckoutOtpChallengeStatus : short
{
    Dispatching = 1,
    Active = 2,
    Synthetic = 3,
    Superseded = 4,
    Verified = 5,
    Failed = 6,
    Expired = 7
}

public sealed class CheckoutSession
{
    private CheckoutSession() { }

    public Guid Id { get; private set; }
    public Guid GatewayPaymentId { get; private set; }
    public CheckoutSessionStatus Status { get; private set; }
    public byte[] TokenNonce { get; private set; } = [];
    public byte[] TokenHash { get; private set; } = [];
    public int TokenKeyVersion { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateTimeOffset? AuthenticatedAt { get; private set; }
    public byte[]? AuthorizationSecretHash { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public bool IsActive => Status is CheckoutSessionStatus.Created
        or CheckoutSessionStatus.Authenticated
        or CheckoutSessionStatus.AwaitingFunding
        or CheckoutSessionStatus.Processing;

    public static CheckoutSession Create(
        Guid id,
        Guid gatewayPaymentId,
        byte[] tokenNonce,
        byte[] tokenHash,
        int tokenKeyVersion,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || gatewayPaymentId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه نشست پرداخت نامعتبر است");
        if (tokenNonce.Length != 32 || tokenHash.Length != 32 || tokenKeyVersion <= 0)
            throw new CreditPaymentsDomainException("اطلاعات امنیتی نشست پرداخت نامعتبر است");
        if (expiresAt <= now)
            throw new CreditPaymentsDomainException("زمان انقضای نشست پرداخت نامعتبر است");
        return new CheckoutSession
        {
            Id = id,
            GatewayPaymentId = gatewayPaymentId,
            Status = CheckoutSessionStatus.Created,
            TokenNonce = tokenNonce.ToArray(),
            TokenHash = tokenHash.ToArray(),
            TokenKeyVersion = tokenKeyVersion,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public void Authenticate(
        Guid customerId,
        byte[] authorizationSecretHash,
        DateTimeOffset authorizationExpiresAt,
        DateTimeOffset now)
    {
        if (customerId == Guid.Empty || authorizationSecretHash.Length != 32)
            throw new CreditPaymentsDomainException("اطلاعات احراز هویت نشست نامعتبر است");
        if (now >= ExpiresAt || authorizationExpiresAt > ExpiresAt || authorizationExpiresAt <= now)
            throw new CreditPaymentsDomainException("نشست پرداخت منقضی شده است");
        if (CustomerId is not null && CustomerId != customerId)
            throw new CreditPaymentsDomainException("مشتری نشست پرداخت قابل تغییر نیست");
        if (Status == CheckoutSessionStatus.Authenticated && CustomerId == customerId) return;
        if (Status != CheckoutSessionStatus.Created)
            throw new CreditPaymentsDomainException("وضعیت نشست برای احراز هویت مجاز نیست");
        CustomerId = customerId;
        Status = CheckoutSessionStatus.Authenticated;
        AuthenticatedAt = now;
        AuthorizationSecretHash = authorizationSecretHash.ToArray();
        AuthorizationExpiresAt = authorizationExpiresAt;
        UpdatedAt = now;
        Version++;
    }

    public void MarkAwaitingFunding(DateTimeOffset now)
    {
        if (Status == CheckoutSessionStatus.AwaitingFunding)
            return;
        if (Status != CheckoutSessionStatus.Authenticated || CustomerId is null)
            throw new CreditPaymentsDomainException("وضعیت نشست برای انتظار تأمین موجودی مجاز نیست");
        Status = CheckoutSessionStatus.AwaitingFunding;
        UpdatedAt = now;
        Version++;
    }

    public void MarkFundingSufficient(DateTimeOffset now)
    {
        if (Status == CheckoutSessionStatus.Authenticated)
            return;
        if (Status != CheckoutSessionStatus.AwaitingFunding || CustomerId is null)
            throw new CreditPaymentsDomainException("وضعیت نشست برای بازگشت از تأمین موجودی مجاز نیست");
        Status = CheckoutSessionStatus.Authenticated;
        UpdatedAt = now;
        Version++;
    }

    public void BeginProcessing(DateTimeOffset now)
    {
        if (Status != CheckoutSessionStatus.Authenticated || CustomerId is null)
            throw new CreditPaymentsDomainException("وضعیت نشست برای پرداخت مجاز نیست");
        Status = CheckoutSessionStatus.Processing;
        UpdatedAt = now;
        Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == CheckoutSessionStatus.Completed) return;
        if (Status != CheckoutSessionStatus.Processing)
            throw new CreditPaymentsDomainException("تکمیل نشست مجاز نیست");
        Status = CheckoutSessionStatus.Completed;
        UpdatedAt = now;
        Version++;
    }

    public void ReturnToAwaitingFunding(DateTimeOffset now)
    {
        if (Status != CheckoutSessionStatus.Processing)
            throw new CreditPaymentsDomainException("بازگشت نشست به تأمین موجودی مجاز نیست");
        Status = CheckoutSessionStatus.AwaitingFunding;
        UpdatedAt = now;
        Version++;
    }

    public void ReturnToAuthenticated(DateTimeOffset now)
    {
        if (Status != CheckoutSessionStatus.Processing)
            throw new CreditPaymentsDomainException("بازگشت نشست به حالت احرازشده مجاز نیست");
        Status = CheckoutSessionStatus.Authenticated;
        UpdatedAt = now;
        Version++;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == CheckoutSessionStatus.Canceled) return;
        if (!IsActive)
            throw new CreditPaymentsDomainException("لغو نشست پرداخت در وضعیت فعلی مجاز نیست");
        Status = CheckoutSessionStatus.Canceled;
        UpdatedAt = now;
        Version++;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == CheckoutSessionStatus.Expired) return;
        if (!IsActive)
            throw new CreditPaymentsDomainException("انقضای نشست پرداخت در وضعیت فعلی مجاز نیست");
        Status = CheckoutSessionStatus.Expired;
        UpdatedAt = now;
        Version++;
    }
}

public sealed class CheckoutOtpChallenge
{
    private CheckoutOtpChallenge() { }

    public Guid Id { get; private set; }
    public Guid CheckoutSessionId { get; private set; }
    public Guid? ResolvedCustomerId { get; private set; }
    public string? ProtectedReferenceCode { get; private set; }
    public short OtpType { get; private set; }
    public CheckoutOtpChallengeStatus Status { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int VerifyAttemptCount { get; private set; }
    public int SendSequence { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CheckoutOtpChallenge Reserve(
        Guid checkoutSessionId,
        Guid? resolvedCustomerId,
        short otpType,
        int sendSequence,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (checkoutSessionId == Guid.Empty || otpType < 0 || sendSequence <= 0 || expiresAt <= now)
            throw new CreditPaymentsDomainException("درخواست کد تأیید نامعتبر است");
        return new CheckoutOtpChallenge
        {
            Id = Guid.NewGuid(),
            CheckoutSessionId = checkoutSessionId,
            ResolvedCustomerId = resolvedCustomerId,
            OtpType = otpType,
            Status = CheckoutOtpChallengeStatus.Dispatching,
            SentAt = now,
            ExpiresAt = expiresAt,
            SendSequence = sendSequence,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Activate(string protectedReferenceCode, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (Status != CheckoutOtpChallengeStatus.Dispatching || ResolvedCustomerId is null)
            throw new CreditPaymentsDomainException("فعال‌سازی کد تأیید مجاز نیست");
        ProtectedReferenceCode = CreditPaymentsText.Required(
            protectedReferenceCode, 4096, "مرجع محافظت‌شده کد تأیید نامعتبر است");
        SetActive(CheckoutOtpChallengeStatus.Active, expiresAt, now);
    }

    public void ActivateSynthetic(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (Status != CheckoutOtpChallengeStatus.Dispatching || ResolvedCustomerId is not null)
            throw new CreditPaymentsDomainException("فعال‌سازی چالش مصنوعی مجاز نیست");
        SetActive(CheckoutOtpChallengeStatus.Synthetic, expiresAt, now);
    }

    public void Supersede(DateTimeOffset now)
    {
        if (Status is CheckoutOtpChallengeStatus.Active or CheckoutOtpChallengeStatus.Synthetic)
        {
            Status = CheckoutOtpChallengeStatus.Superseded;
            UpdatedAt = now;
        }
    }

    public bool RecordVerificationAttempt(int maximumAttempts, DateTimeOffset now)
    {
        if (Status is not (CheckoutOtpChallengeStatus.Active or CheckoutOtpChallengeStatus.Synthetic)
            || now >= ExpiresAt || maximumAttempts <= 0)
            return false;
        if (VerifyAttemptCount >= maximumAttempts)
        {
            Status = CheckoutOtpChallengeStatus.Failed;
            UpdatedAt = now;
            return false;
        }
        VerifyAttemptCount++;
        UpdatedAt = now;
        return true;
    }

    public void Fail(int maximumAttempts, DateTimeOffset now)
    {
        if (VerifyAttemptCount >= maximumAttempts)
            Status = CheckoutOtpChallengeStatus.Failed;
        UpdatedAt = now;
    }

    public void MarkDispatchFailed(DateTimeOffset now)
    {
        if (Status == CheckoutOtpChallengeStatus.Dispatching)
            Status = CheckoutOtpChallengeStatus.Failed;
        UpdatedAt = now;
    }

    public void MarkVerified(DateTimeOffset now)
    {
        if (Status != CheckoutOtpChallengeStatus.Active)
            throw new CreditPaymentsDomainException("تأیید این چالش مجاز نیست");
        Status = CheckoutOtpChallengeStatus.Verified;
        UpdatedAt = now;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (Status is CheckoutOtpChallengeStatus.Active or CheckoutOtpChallengeStatus.Synthetic)
            Status = CheckoutOtpChallengeStatus.Expired;
        UpdatedAt = now;
    }

    private void SetActive(CheckoutOtpChallengeStatus status, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
            throw new CreditPaymentsDomainException("زمان اعتبار کد تأیید نامعتبر است");
        Status = status;
        ExpiresAt = expiresAt;
        UpdatedAt = now;
    }
}
