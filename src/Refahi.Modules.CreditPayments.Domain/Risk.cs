namespace Refahi.Modules.CreditPayments.Domain;

public static class RiskRuleCodes
{
    public const string TerminalDailyPaymentCount = "terminal_daily_payment_count";
    public const string TerminalDailyPaymentAmount = "terminal_daily_payment_amount";
    public const string CustomerDailyPaymentCount = "customer_daily_payment_count";
    public const string CustomerDailyPaymentAmount = "customer_daily_payment_amount";
    public const string TerminalDailyRefundCount = "terminal_daily_refund_count";
    public const string TerminalDailyRefundAmount = "terminal_daily_refund_amount";
    public const string FundingAttemptLimit = "funding_attempt_limit";
}

public static class RiskOperationTypes
{
    public const string PaymentCreate = "payment_create";
    public const string PaymentExecution = "payment_execution";
    public const string RefundCreate = "refund_create";
    public const string FundingStart = "funding_start";
}

public enum RiskScopeType : short { Terminal = 1, Customer = 2 }
public enum RiskDecision : short { Allowed = 1, Denied = 2 }

public sealed class RiskVelocityCounter
{
    private RiskVelocityCounter() { }
    public Guid Id { get; private set; }
    public RiskScopeType ScopeType { get; private set; }
    public Guid ScopeId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public DateTimeOffset BucketStart { get; private set; }
    public long AcceptedCount { get; private set; }
    public long AcceptedAmountMinor { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static RiskVelocityCounter Create(RiskScopeType scopeType, Guid scopeId, string operationType,
        DateTimeOffset bucketStart, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), ScopeType = scopeType, ScopeId = scopeId,
        OperationType = CreditPaymentsText.Required(operationType, 50, "نوع عملیات ریسک نامعتبر است"),
        BucketStart = bucketStart, UpdatedAt = now, Version = 1
    };

    public void Reserve(long amountMinor, DateTimeOffset now)
    {
        if (amountMinor < 0) throw new CreditPaymentsDomainException("مبلغ رزرو ریسک نامعتبر است");
        checked { AcceptedCount++; AcceptedAmountMinor += amountMinor; }
        UpdatedAt = now; Version++;
    }

    public void Release(long amountMinor, DateTimeOffset now)
    {
        if (amountMinor < 0 || AcceptedCount <= 0 || AcceptedAmountMinor < amountMinor)
            throw new CreditPaymentsDomainException("آزادسازی رزرو ریسک نامعتبر است");
        AcceptedCount--; AcceptedAmountMinor -= amountMinor; UpdatedAt = now; Version++;
    }

    public void Rebuild(long acceptedCount, long acceptedAmountMinor, DateTimeOffset now)
    {
        if (acceptedCount < 0 || acceptedAmountMinor < 0)
            throw new CreditPaymentsDomainException("مقادیر بازسازی counter نامعتبر است");
        AcceptedCount = acceptedCount; AcceptedAmountMinor = acceptedAmountMinor;
        UpdatedAt = now; Version++;
    }
}

public sealed class RiskReservation
{
    private RiskReservation() { }
    public Guid Id { get; private set; }
    public Guid CounterId { get; private set; }
    public RiskScopeType ScopeType { get; private set; }
    public Guid ScopeId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateTimeOffset BucketStart { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    public static RiskReservation Create(Guid counterId, RiskScopeType scopeType, Guid scopeId,
        string operationType, Guid resourceId, long amountMinor, DateTimeOffset bucketStart, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CounterId = counterId, ScopeType = scopeType, ScopeId = scopeId,
        OperationType = operationType, ResourceId = resourceId, AmountMinor = amountMinor,
        BucketStart = bucketStart, CreatedAt = now
    };

    public void Reactivate(Guid counterId,long amountMinor,DateTimeOffset bucketStart,DateTimeOffset now)
    {
        CounterId=counterId; AmountMinor=amountMinor; BucketStart=bucketStart;
        ReleasedAt=null; CreatedAt=now;
    }
    public void Release(DateTimeOffset now) { if (ReleasedAt is null) ReleasedAt = now; }
}

public sealed class RiskDecisionRecord
{
    private RiskDecisionRecord() { }
    public Guid Id { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public Guid? MerchantId { get; private set; }
    public Guid? TerminalId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid ResourceId { get; private set; }
    public string RuleCode { get; private set; } = string.Empty;
    public RiskDecision Decision { get; private set; }
    public long? CurrentValue { get; private set; }
    public long? LimitValue { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? CorrelationId { get; private set; }

    public static RiskDecisionRecord Create(string operationType, Guid? merchantId, Guid? terminalId,
        Guid? customerId, Guid resourceId, string ruleCode, RiskDecision decision,
        long? currentValue, long? limitValue, DateTimeOffset now, string? correlationId = null) => new()
    {
        Id = Guid.NewGuid(), OperationType = operationType, MerchantId = merchantId,
        TerminalId = terminalId, CustomerId = customerId, ResourceId = resourceId,
        RuleCode = CreditPaymentsText.Required(ruleCode, 100, "کد قانون ریسک نامعتبر است"),
        Decision = decision, CurrentValue = currentValue, LimitValue = limitValue,
        OccurredAt = now, CorrelationId = correlationId
    };
}
