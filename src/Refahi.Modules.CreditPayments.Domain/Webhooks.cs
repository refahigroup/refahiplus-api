namespace Refahi.Modules.CreditPayments.Domain;

public static class WebhookEventTypes
{
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentFailed = "payment.failed";
    public const string PaymentCanceled = "payment.canceled";
    public const string PaymentExpired = "payment.expired";
    public const string RefundSucceeded = "refund.succeeded";
    public const string RefundFailed = "refund.failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PaymentSucceeded, PaymentFailed, PaymentCanceled, PaymentExpired,
        RefundSucceeded, RefundFailed
    };
}

public enum OutboxMessageStatus : short { Pending = 1, Processing = 2, Completed = 3 }
public enum WebhookDeliveryStatus : short
{
    Pending = 1,
    Processing = 2,
    RetryScheduled = 3,
    Delivered = 4,
    DeadLettered = 5
}

public sealed class WebhookEvent
{
    public const int CurrentVersion = 1;
    public const int MaximumPayloadBytes = 32 * 1024;
    private WebhookEvent() { }

    public Guid Id { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int EventVersion { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid TerminalId { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static WebhookEvent Create(Guid id, string sourceKey, string eventType,
        Guid merchantId, Guid terminalId, string aggregateType, Guid aggregateId,
        DateTimeOffset occurredAt, string payloadJson, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || merchantId == Guid.Empty || terminalId == Guid.Empty || aggregateId == Guid.Empty)
            throw new CreditPaymentsDomainException("شناسه‌های رویداد وب‌هوک نامعتبر است");
        if (!WebhookEventTypes.All.Contains(eventType))
            throw new CreditPaymentsDomainException("نوع رویداد وب‌هوک نامعتبر است");
        if (System.Text.Encoding.UTF8.GetByteCount(payloadJson) > MaximumPayloadBytes)
            throw new CreditPaymentsDomainException("اندازه payload وب‌هوک بیش از حد مجاز است");
        return new WebhookEvent
        {
            Id = id,
            SourceKey = CreditPaymentsText.Required(sourceKey, 200, "کلید منبع رویداد نامعتبر است"),
            EventType = eventType,
            EventVersion = CurrentVersion,
            MerchantId = merchantId,
            TerminalId = terminalId,
            AggregateType = CreditPaymentsText.Required(aggregateType, 50, "نوع aggregate نامعتبر است"),
            AggregateId = aggregateId,
            OccurredAt = occurredAt,
            PayloadJson = CreditPaymentsText.Required(payloadJson, MaximumPayloadBytes, "payload وب‌هوک نامعتبر است"),
            CreatedAt = createdAt
        };
    }
}

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public Guid Id { get; private set; }
    public Guid WebhookEventId { get; private set; }
    public OutboxMessageStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long Version { get; private set; }

    public static OutboxMessage Create(Guid eventId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), WebhookEventId = eventId, Status = OutboxMessageStatus.Pending,
        CreatedAt = now, Version = 1
    };

    public void Claim(string owner, DateTimeOffset until)
    {
        if (Status is not (OutboxMessageStatus.Pending or OutboxMessageStatus.Processing))
            throw new CreditPaymentsDomainException("پیام outbox قابل اجاره نیست");
        Status = OutboxMessageStatus.Processing;
        LeaseOwner = CreditPaymentsText.Required(owner, 100, "مالک lease نامعتبر است");
        LeaseUntil = until;
        Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != OutboxMessageStatus.Processing)
            throw new CreditPaymentsDomainException("تکمیل پیام outbox مجاز نیست");
        Status = OutboxMessageStatus.Completed;
        CompletedAt = now;
        LeaseOwner = null;
        LeaseUntil = null;
        NextRetryAt = null;
        Version++;
    }
}

public sealed class WebhookDelivery
{
    private WebhookDelivery() { }
    public Guid Id { get; private set; }
    public Guid WebhookEventId { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int ManualResendCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public int? LastHttpStatusCode { get; private set; }
    public string? LastFailureCode { get; private set; }
    public int? LastSecretVersionUsed { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static WebhookDelivery Create(Guid eventId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), WebhookEventId = eventId, Status = WebhookDeliveryStatus.Pending,
        NextAttemptAt = now, CreatedAt = now, UpdatedAt = now, Version = 1
    };

    public void Claim(string owner, DateTimeOffset now, DateTimeOffset until)
    {
        if (Status is not (WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.RetryScheduled or WebhookDeliveryStatus.Processing))
            throw new CreditPaymentsDomainException("تحویل وب‌هوک قابل اجاره نیست");
        Status = WebhookDeliveryStatus.Processing;
        AttemptCount++;
        LastAttemptAt = now;
        NextAttemptAt = null;
        LeaseOwner = CreditPaymentsText.Required(owner, 100, "مالک lease نامعتبر است");
        LeaseUntil = until;
        UpdatedAt = now;
        Version++;
    }

    public void MarkDelivered(int httpStatusCode, int secretVersion, DateTimeOffset now)
    {
        RequireProcessing();
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAt = now;
        LastHttpStatusCode = httpStatusCode;
        LastFailureCode = null;
        LastSecretVersionUsed = secretVersion;
        ClearLease(); UpdatedAt = now; Version++;
    }

    public void ScheduleRetry(string failureCode, int? httpStatusCode, int? secretVersion,
        DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        RequireProcessing();
        Status = WebhookDeliveryStatus.RetryScheduled;
        LastFailureCode = CreditPaymentsText.Required(failureCode, 100, "کد خطای تحویل نامعتبر است");
        LastHttpStatusCode = httpStatusCode;
        LastSecretVersionUsed = secretVersion;
        NextAttemptAt = nextAttemptAt;
        ClearLease(); UpdatedAt = now; Version++;
    }

    public void DeadLetter(string failureCode, int? httpStatusCode, int? secretVersion, DateTimeOffset now)
    {
        RequireProcessing();
        Status = WebhookDeliveryStatus.DeadLettered;
        LastFailureCode = CreditPaymentsText.Required(failureCode, 100, "کد خطای تحویل نامعتبر است");
        LastHttpStatusCode = httpStatusCode;
        LastSecretVersionUsed = secretVersion;
        NextAttemptAt = null;
        ClearLease(); UpdatedAt = now; Version++;
    }

    public void ManualResend(DateTimeOffset now)
    {
        if (Status != WebhookDeliveryStatus.DeadLettered)
            throw new CreditPaymentsDomainException("ارسال مجدد فقط برای تحویل dead-letter مجاز است");
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        ManualResendCount++;
        NextAttemptAt = now;
        LastFailureCode = null;
        LastHttpStatusCode = null;
        ClearLease(); UpdatedAt = now; Version++;
    }

    private void RequireProcessing()
    {
        if (Status != WebhookDeliveryStatus.Processing)
            throw new CreditPaymentsDomainException("نتیجه تحویل وب‌هوک در وضعیت فعلی مجاز نیست");
    }
    private void ClearLease() { LeaseOwner = null; LeaseUntil = null; }
}
