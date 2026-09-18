namespace Refahi.Modules.CreditPayments.Domain;

public enum GatewayPaymentStatus : short
{
    Created = 1,
    AwaitingCustomer = 2,
    AwaitingFunding = 3,
    Processing = 4,
    Succeeded = 5,
    Failed = 6,
    Canceled = 7,
    Expired = 8,
    PartiallyRefunded = 9,
    Refunded = 10
}

public enum IdempotencyState : short
{
    InProgress = 1,
    Completed = 2
}

public static class IdempotencyOperationTypes
{
    public const string CreatePayment = "CreatePayment";
    public const string CancelPayment = "CancelPayment";
    public const string CreateRefund = "CreateRefund";
}

/// <summary>
/// One immutable merchant request to collect one fixed IRR amount. A newly-created
/// payment remains Created until Phase 04 durably establishes its CheckoutSession.
/// </summary>
public sealed class GatewayPayment
{
    private GatewayPayment() { }

    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid TerminalId { get; private set; }
    public string MerchantOrderId { get; private set; } = string.Empty;
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public GatewayPaymentStatus Status { get; private set; }
    public string ReturnUrl { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string MerchantDisplayNameSnapshot { get; private set; } = string.Empty;
    public string TerminalNameSnapshot { get; private set; } = string.Empty;
    public string ApprovedCategoryCodeSnapshot { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTimeOffset? OrderBoundAt { get; private set; }
    public long CapturedAmountMinor { get; private set; }
    public long RefundedAmountMinor { get; private set; }
    public Guid? WalletPaymentId { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? PolicyId { get; private set; }
    public string? PolicyVersion { get; private set; }
    public string? PolicyHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public void ActivateCheckout(DateTimeOffset now)
    {
        if (Status != GatewayPaymentStatus.Created)
            throw new CreditPaymentsDomainException("فعال‌سازی پرداخت برای درگاه مجاز نیست");
        Status = GatewayPaymentStatus.AwaitingCustomer;
        UpdatedAt = now;
        Version++;
    }

    public void BindCustomer(Guid customerId, DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه مشتری نامعتبر است");
        if (Status != GatewayPaymentStatus.AwaitingCustomer)
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای اتصال مشتری مجاز نیست");
        if (CustomerId is not null && CustomerId != customerId)
            throw new CreditPaymentsDomainException("مشتری پرداخت قابل تغییر نیست");
        if (CustomerId == customerId) return;
        CustomerId = customerId;
        UpdatedAt = now;
        Version++;
    }

    public void BindOrder(Guid orderId, DateTimeOffset now)
    {
        if (orderId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه سفارش نامعتبر است");
        if (CustomerId is null)
            throw new CreditPaymentsDomainException("اتصال سفارش پیش از احراز مشتری مجاز نیست");
        if (Status is not (GatewayPaymentStatus.AwaitingCustomer or GatewayPaymentStatus.AwaitingFunding))
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای اتصال سفارش مجاز نیست");
        if (OrderId is not null && OrderId != orderId)
            throw new CreditPaymentsDomainException("سفارش متصل به پرداخت قابل تغییر نیست");
        if (OrderId == orderId)
            return;

        OrderId = orderId;
        OrderBoundAt = now;
        UpdatedAt = now;
        Version++;
    }

    public void MarkAwaitingFunding(DateTimeOffset now)
    {
        if (Status == GatewayPaymentStatus.AwaitingFunding)
            return;
        if (Status != GatewayPaymentStatus.AwaitingCustomer || OrderId is null)
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای انتظار تأمین موجودی مجاز نیست");
        Status = GatewayPaymentStatus.AwaitingFunding;
        UpdatedAt = now;
        Version++;
    }

    public void MarkFundingSufficient(DateTimeOffset now)
    {
        if (Status == GatewayPaymentStatus.AwaitingCustomer)
            return;
        if (Status != GatewayPaymentStatus.AwaitingFunding || OrderId is null)
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای بازگشت از تأمین موجودی مجاز نیست");
        Status = GatewayPaymentStatus.AwaitingCustomer;
        UpdatedAt = now;
        Version++;
    }

    public void BeginProcessing(
        string policyId,
        string policyVersion,
        string policyHash,
        DateTimeOffset now)
    {
        if (Status != GatewayPaymentStatus.AwaitingCustomer || CustomerId is null || OrderId is null)
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای اجرا مجاز نیست");
        PolicyId = CreditPaymentsText.Required(policyId, 100, "شناسه سیاست مالی نامعتبر است");
        PolicyVersion = CreditPaymentsText.Required(policyVersion, 100, "نسخه سیاست مالی نامعتبر است");
        PolicyHash = CreditPaymentsText.Required(policyHash, 64, "هش سیاست مالی نامعتبر است");
        Status = GatewayPaymentStatus.Processing;
        UpdatedAt = now;
        Version++;
    }

    public void MarkSucceeded(Guid walletPaymentId, DateTimeOffset paidAt, DateTimeOffset now)
    {
        if (Status == GatewayPaymentStatus.Succeeded)
        {
            if (WalletPaymentId != walletPaymentId)
                throw new CreditPaymentsDomainException("مرجع مالی پرداخت قابل تغییر نیست");
            return;
        }
        if (Status != GatewayPaymentStatus.Processing || walletPaymentId == Guid.Empty)
            throw new CreditPaymentsDomainException("تکمیل پرداخت مجاز نیست");
        Status = GatewayPaymentStatus.Succeeded;
        CapturedAmountMinor = AmountMinor;
        WalletPaymentId = walletPaymentId;
        PaidAt = paidAt;
        UpdatedAt = now;
        Version++;
    }

    public void ApplySuccessfulRefund(long amountMinor, DateTimeOffset now)
    {
        if (Status is not (GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded))
            throw new CreditPaymentsDomainException("وضعیت پرداخت برای بازگشت وجه مجاز نیست");
        if (amountMinor <= 0 || CapturedAmountMinor <= 0 ||
            RefundedAmountMinor > CapturedAmountMinor - amountMinor)
            throw new CreditPaymentsDomainException("مبلغ بازگشت وجه از مبلغ قابل بازگشت بیشتر است");
        RefundedAmountMinor += amountMinor;
        Status = RefundedAmountMinor == CapturedAmountMinor
            ? GatewayPaymentStatus.Refunded
            : GatewayPaymentStatus.PartiallyRefunded;
        UpdatedAt = now;
        Version++;
    }

    public void ReturnToAwaitingFunding(DateTimeOffset now)
    {
        if (Status != GatewayPaymentStatus.Processing)
            throw new CreditPaymentsDomainException("بازگشت به تأمین موجودی مجاز نیست");
        Status = GatewayPaymentStatus.AwaitingFunding;
        UpdatedAt = now;
        Version++;
    }

    public void ReturnToAwaitingCustomer(DateTimeOffset now)
    {
        if (Status != GatewayPaymentStatus.Processing)
            throw new CreditPaymentsDomainException("بازگشت به انتظار مشتری مجاز نیست");
        Status = GatewayPaymentStatus.AwaitingCustomer;
        UpdatedAt = now;
        Version++;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == GatewayPaymentStatus.Canceled) return;
        if (Status is not (GatewayPaymentStatus.Created or GatewayPaymentStatus.AwaitingCustomer
            or GatewayPaymentStatus.AwaitingFunding or GatewayPaymentStatus.Processing))
            throw new CreditPaymentsDomainException("لغو پرداخت در وضعیت فعلی مجاز نیست");
        Status = GatewayPaymentStatus.Canceled;
        UpdatedAt = now;
        Version++;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == GatewayPaymentStatus.Expired) return;
        if (now < ExpiresAt)
            throw new CreditPaymentsDomainException("زمان انقضای پرداخت نرسیده است");
        if (Status is not (GatewayPaymentStatus.Created or GatewayPaymentStatus.AwaitingCustomer
            or GatewayPaymentStatus.AwaitingFunding or GatewayPaymentStatus.Processing))
            throw new CreditPaymentsDomainException("انقضای پرداخت در وضعیت فعلی مجاز نیست");
        Status = GatewayPaymentStatus.Expired;
        UpdatedAt = now;
        Version++;
    }

    public static GatewayPayment Create(
        Guid merchantId,
        Guid terminalId,
        string merchantOrderId,
        long amountMinor,
        string currency,
        string? description,
        string metadataJson,
        string canonicalReturnUrl,
        DateTimeOffset expiresAt,
        string merchantDisplayNameSnapshot,
        string terminalNameSnapshot,
        string approvedCategoryCodeSnapshot,
        DateTimeOffset now)
    {
        if (merchantId == Guid.Empty || terminalId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه پذیرنده و پایانه الزامی است");
        if (amountMinor <= 0)
            throw new CreditPaymentsDomainException("مبلغ باید مثبت باشد");
        if (!string.Equals(currency, "IRR", StringComparison.Ordinal))
            throw new CreditPaymentsDomainException("ارز پشتیبانی نمی‌شود");
        if (expiresAt <= now)
            throw new CreditPaymentsDomainException("زمان انقضای پرداخت نامعتبر است");

        var normalizedDescription = description?.Trim();
        if (normalizedDescription?.Length == 0) normalizedDescription = null;
        if (normalizedDescription?.Length > 250)
            throw new CreditPaymentsDomainException("توضیحات حداکثر ۲۵۰ نویسه است");

        return new GatewayPayment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            TerminalId = terminalId,
            MerchantOrderId = CreditPaymentsText.Required(merchantOrderId, 100, "شناسه سفارش پذیرنده نامعتبر است"),
            AmountMinor = amountMinor,
            Currency = currency,
            Description = normalizedDescription,
            MetadataJson = CreditPaymentsText.Required(metadataJson, 4096, "فراداده نامعتبر است"),
            Status = GatewayPaymentStatus.Created,
            ReturnUrl = CreditPaymentsText.Required(canonicalReturnUrl, 2048, "نشانی بازگشت نامعتبر است"),
            ExpiresAt = expiresAt,
            MerchantDisplayNameSnapshot = CreditPaymentsText.Required(merchantDisplayNameSnapshot, 200, "نام نمایشی پذیرنده الزامی است"),
            TerminalNameSnapshot = CreditPaymentsText.Required(terminalNameSnapshot, 150, "نام پایانه الزامی است"),
            ApprovedCategoryCodeSnapshot = CreditPaymentsText.Category(approvedCategoryCodeSnapshot),
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }
}

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public Guid Id { get; private set; }
    public Guid TerminalId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public byte[] RequestHash { get; private set; } = [];
    public int RequestHashVersion { get; private set; }
    public Guid? ResourceId { get; private set; }
    public IdempotencyState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static IdempotencyRecord Claim(Guid terminalId, string operationType, string key, byte[] requestHash, DateTimeOffset now)
    {
        if (terminalId == Guid.Empty) throw new CreditPaymentsDomainException("پایانه الزامی است");
        if (requestHash.Length != 32) throw new CreditPaymentsDomainException("هش درخواست نامعتبر است");
        return new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            TerminalId = terminalId,
            OperationType = CreditPaymentsText.Required(operationType, 50, "نوع عملیات نامعتبر است"),
            IdempotencyKey = ValidateKey(key),
            RequestHash = requestHash.ToArray(),
            RequestHashVersion = 1,
            State = IdempotencyState.InProgress,
            CreatedAt = now
        };
    }

    public void Complete(Guid resourceId, DateTimeOffset now)
    {
        if (State != IdempotencyState.InProgress || resourceId == Guid.Empty)
            throw new CreditPaymentsDomainException("تکمیل درخواست تکرارپذیر مجاز نیست");
        ResourceId = resourceId;
        State = IdempotencyState.Completed;
        CompletedAt = now;
    }

    public static string ValidateKey(string value)
    {
        if (value is null || value.Length is < 8 or > 128 || value.Any(c => c is < (char)33 or > (char)126))
            throw new CreditPaymentsDomainException("کلید تکرارپذیری باید ۸ تا ۱۲۸ نویسه ASCII قابل‌نمایش و بدون فاصله باشد");
        return value;
    }
}

public enum PaymentCreationPersistenceOutcome
{
    Created,
    Existing,
    IdempotencyConflict,
    MerchantOrderConflict,
    RiskDenied
}

public sealed record PaymentCreationPersistenceResult(
    PaymentCreationPersistenceOutcome Outcome,
    GatewayPayment? Payment,
    CheckoutSession? CheckoutSession,
    string? RiskRuleCode = null);
