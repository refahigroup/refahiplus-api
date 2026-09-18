namespace Refahi.Modules.CreditPayments.Domain;

public enum CreditPaymentIntegrationEventStatus : short
{
    Pending = 1,
    Processing = 2,
    Published = 3
}

public sealed class CreditPaymentIntegrationEvent
{
    private CreditPaymentIntegrationEvent() { }

    public Guid Id { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int EventVersion { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public CreditPaymentIntegrationEventStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? LastFailureCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static CreditPaymentIntegrationEvent Create(Guid eventId, string sourceKey,
        string eventType, int eventVersion, string source, string aggregateType, Guid aggregateId,
        DateTimeOffset occurredAt, string payloadJson, DateTimeOffset now)
    {
        if (eventId == Guid.Empty || aggregateId == Guid.Empty || eventVersion <= 0)
            throw new CreditPaymentsDomainException("مشخصات رویداد یکپارچه‌سازی نامعتبر است");
        return new CreditPaymentIntegrationEvent
        {
            Id = eventId,
            SourceKey = CreditPaymentsText.Required(sourceKey, 200, "کلید منبع رویداد نامعتبر است"),
            EventType = CreditPaymentsText.Required(eventType, 100, "نوع رویداد نامعتبر است"),
            EventVersion = eventVersion,
            Source = CreditPaymentsText.Required(source, 50, "منبع رویداد نامعتبر است"),
            AggregateType = CreditPaymentsText.Required(aggregateType, 50, "نوع aggregate نامعتبر است"),
            AggregateId = aggregateId,
            OccurredAt = occurredAt,
            PayloadJson = CreditPaymentsText.Required(payloadJson, 32768, "payload رویداد نامعتبر است"),
            Status = CreditPaymentIntegrationEventStatus.Pending,
            NextRetryAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public void AcquireLease(string owner, DateTimeOffset until, DateTimeOffset now)
    {
        if (Status == CreditPaymentIntegrationEventStatus.Published || until <= now)
            throw new CreditPaymentsDomainException("lease رویداد یکپارچه‌سازی نامعتبر است");
        LeaseOwner = CreditPaymentsText.Required(owner, 100, "مالک lease نامعتبر است");
        LeaseUntil = until;
        Status = CreditPaymentIntegrationEventStatus.Processing;
        UpdatedAt = now;
        Version++;
    }

    public void MarkPublished(string owner, DateTimeOffset now)
    {
        EnsureOwner(owner, now);
        Status = CreditPaymentIntegrationEventStatus.Published;
        PublishedAt = now;
        NextRetryAt = null;
        LastFailureCode = null;
        ClearLease();
        UpdatedAt = now;
        Version++;
    }

    public void ScheduleRetry(string owner, string failureCode, DateTimeOffset nextRetryAt, DateTimeOffset now)
    {
        EnsureOwner(owner, now);
        Status = CreditPaymentIntegrationEventStatus.Pending;
        RetryCount++;
        NextRetryAt = nextRetryAt;
        LastFailureCode = CreditPaymentsText.Required(failureCode, 100, "کد خطای انتشار نامعتبر است");
        ClearLease();
        UpdatedAt = now;
        Version++;
    }

    private void EnsureOwner(string owner, DateTimeOffset now)
    {
        if (Status != CreditPaymentIntegrationEventStatus.Processing || LeaseOwner != owner || LeaseUntil <= now)
            throw new CreditPaymentsDomainException("مالکیت انتشار رویداد معتبر نیست");
    }

    private void ClearLease() { LeaseOwner = null; LeaseUntil = null; }
}
