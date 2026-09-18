namespace Refahi.Modules.CreditPayments.Domain;

public enum ReconciliationKind : short
{
    Funding = 1,
    PaymentExecution = 2,
    Refund = 3,
    InternalIntegrity = 4,
    WebhookIntegrity = 5,
    RiskIntegrity = 6
}

public enum ReconciliationRunStatus : short
{
    Running = 1,
    Completed = 2,
    CompletedWithFindings = 3,
    Failed = 4
}

public enum ReconciliationSeverity : short { Info = 1, Warning = 2, Critical = 3 }
public enum ReconciliationFindingStatus : short { Open = 1, Acknowledged = 2, Resolved = 3 }
public enum ReconciliationResolutionCategory : short
{
    None = 0,
    ExistingRecovery = 1,
    RiskCounterRebuild = 2,
    WebhookReconciliation = 3,
    ExternalConvergence = 4,
    OperationalAction = 5
}

public static class ReconciliationFindingCodes
{
    public const string FundingStatusStale = "funding_status_stale";
    public const string FundingSessionMismatch = "funding_session_mismatch";
    public const string FundingCustomerMismatch = "funding_customer_mismatch";
    public const string FundingWalletMismatch = "funding_wallet_mismatch";
    public const string FundingAmountMismatch = "funding_amount_mismatch";
    public const string FundingClientReferenceMismatch = "funding_client_reference_mismatch";
    public const string PaymentProcessingStale = "payment_processing_stale";
    public const string PaymentOrderMissing = "payment_order_missing";
    public const string PaymentOrderBindingMismatch = "payment_order_binding_mismatch";
    public const string PaymentCaptureStateMismatch = "payment_capture_state_mismatch";
    public const string PaymentWalletReferenceMismatch = "payment_wallet_reference_mismatch";
    public const string TerminalStateCaptureConflict = "terminal_state_capture_conflict";
    public const string RefundRecoveryStale = "refund_recovery_stale";
    public const string RefundWalletStatusMismatch = "refund_wallet_status_mismatch";
    public const string RefundWalletReferenceMismatch = "refund_wallet_reference_mismatch";
    public const string RefundAmountMismatch = "refund_amount_mismatch";
    public const string PaymentRefundedTotalMismatch = "payment_refunded_total_mismatch";
    public const string OrderFullRefundProjectionStale = "order_full_refund_projection_stale";
    public const string CheckoutStateMismatch = "checkout_state_mismatch";
    public const string PaymentAttemptStateMismatch = "payment_attempt_state_mismatch";
    public const string TerminationStateMismatch = "termination_state_mismatch";
    public const string FundingContinuationStateMismatch = "funding_continuation_state_mismatch";
    public const string RefundAggregateMismatch = "refund_aggregate_mismatch";
    public const string MissingWebhookEvent = "missing_webhook_event";
    public const string MissingOutbox = "missing_outbox";
    public const string WebhookDeliveryDeadLettered = "webhook_delivery_dead_lettered";
    public const string MissingFinancialCaptureEvent = "missing_settlement_capture_event";
    public const string MissingFinancialRefundEvent = "missing_settlement_refund_event";
    public const string FinancialEventContractDataInvalid = "settlement_event_contract_data_invalid";
    public const string RiskCounterDrift = "risk_counter_drift";
    public const string RiskReservationMismatch = "risk_reservation_mismatch";
}

public sealed class ReconciliationRun
{
    private ReconciliationRun() { }
    public Guid Id { get; private set; }
    public ReconciliationKind Kind { get; private set; }
    public ReconciliationRunStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string WorkerInstanceId { get; private set; } = string.Empty;
    public DateTimeOffset LeaseUntil { get; private set; }
    public DateTimeOffset? RangeFrom { get; private set; }
    public DateTimeOffset? RangeTo { get; private set; }
    public bool DryRun { get; private set; }
    public int ScannedCount { get; private set; }
    public int FindingCount { get; private set; }
    public int AutoRemediationCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string? Cursor { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long Version { get; private set; }

    public static ReconciliationRun Start(ReconciliationKind kind, string worker, DateTimeOffset? from,
        DateTimeOffset? to, bool dryRun, DateTimeOffset leaseUntil, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, Status = ReconciliationRunStatus.Running,
        WorkerInstanceId = CreditPaymentsText.Required(worker, 100, "شناسه worker نامعتبر است"),
        RangeFrom = from, RangeTo = to, DryRun = dryRun, StartedAt = now, LeaseUntil = leaseUntil,
        CreatedAt = now, Version = 1
    };

    public void Complete(int scanned, int findings, int remediations, int errors, string? cursor, DateTimeOffset now)
    {
        RequireRunning();
        if (scanned < 0 || findings < 0 || remediations < 0 || errors < 0)
            throw new CreditPaymentsDomainException("شمارنده‌های reconciliation نامعتبر است");
        ScannedCount = scanned; FindingCount = findings; AutoRemediationCount = remediations;
        ErrorCount = errors; Cursor = Bound(cursor, 500); CompletedAt = now;
        Status = findings > 0 ? ReconciliationRunStatus.CompletedWithFindings : ReconciliationRunStatus.Completed;
        Version++;
    }

    public void Fail(int scanned, int findings, int remediations, int errors, string failureCode, DateTimeOffset now)
    {
        RequireRunning(); ScannedCount = Math.Max(0, scanned); FindingCount = Math.Max(0, findings);
        AutoRemediationCount = Math.Max(0, remediations); ErrorCount = Math.Max(1, errors);
        FailureCode = CreditPaymentsText.Required(failureCode, 100, "کد خطای run نامعتبر است");
        CompletedAt = now; Status = ReconciliationRunStatus.Failed; Version++;
    }

    private void RequireRunning()
    {
        if (Status != ReconciliationRunStatus.Running)
            throw new CreditPaymentsDomainException("run خاتمه‌یافته قابل تغییر نیست");
    }
    private static string? Bound(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximum)];
}

public sealed class ReconciliationFinding
{
    private ReconciliationFinding() { }
    public Guid Id { get; private set; }
    public Guid? ReconciliationRunId { get; private set; }
    public ReconciliationKind Kind { get; private set; }
    public string FindingCode { get; private set; } = string.Empty;
    public ReconciliationSeverity Severity { get; private set; }
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid? MerchantId { get; private set; }
    public Guid? TerminalId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public ReconciliationFindingStatus Status { get; private set; }
    public DateTimeOffset FirstObservedAt { get; private set; }
    public DateTimeOffset LastObservedAt { get; private set; }
    public int ObservationCount { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public ReconciliationResolutionCategory ResolutionCategory { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public string? AcknowledgementNote { get; private set; }
    public string? AutoRemediationAction { get; private set; }
    public int AutoRemediationAttemptCount { get; private set; }
    public DateTimeOffset? LastAutoRemediationAt { get; private set; }
    public DateTimeOffset? NextAutoRemediationAt { get; private set; }
    public string EvidenceJson { get; private set; } = "{}";
    public string EvidenceHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static ReconciliationFinding Create(Guid runId, ReconciliationKind kind, string code,
        ReconciliationSeverity severity, string resourceType, Guid resourceId, Guid? merchantId,
        Guid? terminalId, Guid? customerId, string evidenceJson, string evidenceHash, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), ReconciliationRunId = runId, Kind = kind,
        FindingCode = CreditPaymentsText.Required(code, 100, "کد finding نامعتبر است"), Severity = severity,
        ResourceType = CreditPaymentsText.Required(resourceType, 50, "نوع منبع finding نامعتبر است"),
        ResourceId = resourceId, MerchantId = merchantId, TerminalId = terminalId, CustomerId = customerId,
        Status = ReconciliationFindingStatus.Open, FirstObservedAt = now, LastObservedAt = now,
        ObservationCount = 1, EvidenceJson = BoundRequired(evidenceJson, 8192),
        EvidenceHash = CreditPaymentsText.Required(evidenceHash, 64, "هش evidence نامعتبر است"),
        CreatedAt = now, UpdatedAt = now, Version = 1
    };

    public void Observe(Guid runId, ReconciliationSeverity severity, string evidenceJson, string evidenceHash, DateTimeOffset now)
    {
        if (Status == ReconciliationFindingStatus.Resolved)
            throw new CreditPaymentsDomainException("finding حل‌شده قابل مشاهده مجدد نیست");
        ReconciliationRunId = runId; Severity = severity; LastObservedAt = now; ObservationCount++;
        EvidenceJson = BoundRequired(evidenceJson, 8192); EvidenceHash = evidenceHash; UpdatedAt = now; Version++;
    }

    public void Acknowledge(string actor, string? note, DateTimeOffset now)
    {
        if (Status == ReconciliationFindingStatus.Resolved)
            throw new CreditPaymentsDomainException("finding حل‌شده قابل acknowledge نیست");
        Status = ReconciliationFindingStatus.Acknowledged;
        AcknowledgedBy = CreditPaymentsText.Required(actor, 100, "شناسه کاربر نامعتبر است");
        AcknowledgementNote = string.IsNullOrWhiteSpace(note) ? null : BoundRequired(note.Trim(), 500);
        AcknowledgedAt = now; UpdatedAt = now; Version++;
    }

    public void Resolve(ReconciliationResolutionCategory category, Guid runId, DateTimeOffset now)
    {
        if (Status == ReconciliationFindingStatus.Resolved) return;
        Status = ReconciliationFindingStatus.Resolved; ResolutionCategory = category;
        ReconciliationRunId = runId; ResolvedAt = now; UpdatedAt = now; Version++;
    }

    public void RecordRemediation(string action, DateTimeOffset nextEligibleAt, DateTimeOffset now)
    {
        if (Status == ReconciliationFindingStatus.Resolved)
            throw new CreditPaymentsDomainException("finding حل‌شده قابل remediation نیست");
        AutoRemediationAction = CreditPaymentsText.Required(action, 100, "عملیات remediation نامعتبر است");
        AutoRemediationAttemptCount++; LastAutoRemediationAt = now; NextAutoRemediationAt = nextEligibleAt;
        UpdatedAt = now; Version++;
    }

    private static string BoundRequired(string value, int maximum)
    {
        var normalized = CreditPaymentsText.Required(value, maximum, "مقدار evidence نامعتبر است");
        return normalized;
    }
}
