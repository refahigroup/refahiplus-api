namespace Refahi.Modules.CreditPayments.Domain;

public interface ICreditPaymentsRepository
{
    Task<Merchant?> GetMerchantAsync(Guid id, bool tracking, CancellationToken ct);
    Task<Merchant?> GetMerchantByNationalIdAsync(MerchantEntityType entityType, string nationalId, bool tracking, CancellationToken ct);
    Task<bool> TryCreateMerchantAsync(Merchant merchant, CancellationToken ct);
    Task<Terminal?> GetTerminalAsync(Guid id, bool tracking, CancellationToken ct);
    Task<TerminalCredential?> GetCredentialAsync(Guid id, bool tracking, CancellationToken ct);
    Task<(TerminalCredential Credential, Terminal Terminal, Merchant Merchant)?> GetAuthenticationGraphAsync(string clientId, CancellationToken ct);
    void Add(Merchant merchant);
    void Add(Terminal terminal);
    void Add(TerminalReturnUrl returnUrl);
    void Add(TerminalCredential credential);
    void Add(WebhookEndpoint webhook);
    void AddAudit(AuditRecord audit);
    Task SaveChangesAsync(CancellationToken ct);
    Task<bool> TrySaveManagementChangesAsync(CancellationToken ct);
    Task<bool> TrySaveTerminalCreationAsync(CancellationToken ct);
    Task RecordAuthenticationFailureAsync(string action,Guid? credentialId,Guid? terminalId,
        DateTimeOffset now,CancellationToken ct)=>Task.CompletedTask;
}

public interface IGatewayPaymentRepository
{
    Task<IdempotencyRecord?> GetIdempotencyRecordAsync(Guid terminalId, string operationType, string idempotencyKey, CancellationToken ct);
    Task<GatewayPayment?> GetGatewayPaymentAsync(Guid paymentId, Guid merchantId, Guid terminalId, CancellationToken ct);
    Task<PaymentCreationPersistenceResult> CreateGatewayPaymentAsync(
        GatewayPayment payment,
        CheckoutSession checkoutSession,
        IdempotencyRecord idempotency,
        IReadOnlyCollection<AuditRecord> audits,
        CancellationToken ct);
    Task<PaymentCreationPersistenceResult> CreateGatewayPaymentAsync(
        GatewayPayment payment,
        CheckoutSession checkoutSession,
        IdempotencyRecord idempotency,
        IReadOnlyCollection<AuditRecord> audits,
        RiskReservationRequest? risk,
        CancellationToken ct)=>CreateGatewayPaymentAsync(payment,checkoutSession,idempotency,audits,ct);
}

public sealed record CheckoutLookup(CheckoutSession Session, GatewayPayment Payment);

public sealed record OtpChallengeDispatch(
    CheckoutSession Session,
    GatewayPayment Payment,
    CheckoutOtpChallenge Challenge);

public sealed record FundingEvaluationContext(
    CheckoutSession Session,
    GatewayPayment Payment,
    Merchant Merchant);

public sealed record FundingStatePersistenceResult(
    CheckoutSession Session,
    GatewayPayment Payment,
    bool FundingBecameSufficient);

public sealed record FundingContinuationContext(
    CheckoutSession Session,
    GatewayPayment Payment,
    FundingContinuation Continuation);

public sealed record PaymentExecutionContext(
    CheckoutSession Session,
    GatewayPayment Payment,
    PaymentAttempt? ActiveAttempt,
    FundingContinuation? LatestFundingContinuation,
    PaymentTermination? Termination = null);

public enum PaymentExecutionClaimOutcome
{
    Claimed,
    Processing,
    Succeeded,
    NotReady,
    RiskDenied
}

public sealed record PaymentExecutionClaimResult(
    PaymentExecutionClaimOutcome Outcome,
    PaymentExecutionContext Context,
    string? RiskRuleCode = null);

public enum FundingContinuationClaimOutcome
{
    Created,
    Existing,
    AttemptLimitReached,
    RiskDenied
}

public sealed record FundingContinuationClaimResult(
    FundingContinuationClaimOutcome Outcome,
    FundingContinuation? Continuation,
    string? RiskRuleCode = null);

public interface ICheckoutRepository
{
    Task<CheckoutLookup?> FindCheckoutAsync(byte[] tokenHash, bool tracking, CancellationToken ct);
    Task<CheckoutSession?> GetCheckoutForPaymentAsync(Guid paymentId, CancellationToken ct);
    Task<OtpChallengeDispatch> ReserveOtpChallengeAsync(
        byte[] tokenHash,
        Guid? resolvedCustomerId,
        short otpType,
        DateTimeOffset now,
        DateTimeOffset provisionalExpiresAt,
        TimeSpan resendCooldown,
        int maximumSends,
        CancellationToken ct);
    Task FinalizeOtpChallengeAsync(
        Guid challengeId,
        string? protectedReferenceCode,
        DateTimeOffset expiresAt,
        bool synthetic,
        DateTimeOffset now,
        CancellationToken ct);
    Task MarkOtpDispatchFailedAsync(Guid challengeId, DateTimeOffset now, CancellationToken ct);
    Task<OtpChallengeDispatch?> BeginOtpVerificationAsync(
        byte[] tokenHash,
        Guid challengeId,
        DateTimeOffset now,
        int maximumAttempts,
        CancellationToken ct);
    Task RecordOtpValidationFailureAsync(
        Guid challengeId,
        int maximumAttempts,
        DateTimeOffset now,
        string auditOutcome,
        CancellationToken ct);
    Task AuthenticateCheckoutAsync(
        byte[] tokenHash,
        Guid challengeId,
        Guid customerId,
        byte[] authorizationSecretHash,
        DateTimeOffset authorizationExpiresAt,
        DateTimeOffset now,
        CancellationToken ct);
    Task<FundingEvaluationContext?> GetFundingEvaluationContextAsync(
        byte[] tokenHash,
        CancellationToken ct);
    Task BindOrderAsync(
        Guid checkoutSessionId,
        Guid gatewayPaymentId,
        Guid customerId,
        Guid orderId,
        DateTimeOffset now,
        bool recovered,
        CancellationToken ct);
    Task<FundingStatePersistenceResult> ApplyFundingEvaluationAsync(
        Guid checkoutSessionId,
        Guid gatewayPaymentId,
        Guid customerId,
        Guid orderId,
        bool isSufficient,
        long eligibleAmountMinor,
        long deficitAmountMinor,
        DateTimeOffset evaluatedAt,
        CancellationToken ct);
    Task RecordFundingIntegrityFailureAsync(
        Guid gatewayPaymentId,
        Guid customerId,
        DateTimeOffset now,
        CancellationToken ct);
}

public interface IFundingContinuationRepository
{
    Task<FundingContinuationClaimResult> CreateOrGetFundingContinuationAsync(
        Guid checkoutSessionId,
        Guid gatewayPaymentId,
        Guid customerId,
        Guid orderId,
        Guid personalWalletId,
        long requiredDeficitMinor,
        int maximumAttempts,
        DateTimeOffset now,
        CancellationToken ct);
    Task<FundingContinuationClaimResult> CreateOrGetFundingContinuationAsync(
        Guid checkoutSessionId,Guid gatewayPaymentId,Guid customerId,Guid orderId,
        Guid personalWalletId,long requiredDeficitMinor,int maximumAttempts,
        RiskReservationRequest? risk,
        DateTimeOffset now,
        CancellationToken ct)=>CreateOrGetFundingContinuationAsync(checkoutSessionId,gatewayPaymentId,customerId,
            orderId,personalWalletId,requiredDeficitMinor,maximumAttempts,now,ct);
    Task<FundingContinuationContext?> GetFundingContinuationContextAsync(
        byte[] tokenHash,
        CancellationToken ct);
    Task<FundingContinuationContext?> GetFundingContinuationContextAsync(
        Guid fundingContinuationId,
        CancellationToken ct);
    Task BindWalletFundingSessionAsync(
        Guid fundingContinuationId,
        Guid walletFundingSessionId,
        DateTimeOffset now,
        CancellationToken ct);
    Task MarkFundingContinuationPendingAsync(
        Guid fundingContinuationId,
        DateTimeOffset now,
        DateTimeOffset nextRetryAt,
        CancellationToken ct);
    Task CompleteFundingContinuationAsync(
        Guid fundingContinuationId,
        FundingContinuationStatus status,
        DateTimeOffset now,
        CancellationToken ct);
    Task SchedulePostFundingEvaluationAsync(
        Guid fundingContinuationId,
        DateTimeOffset now,
        DateTimeOffset nextRetryAt,
        CancellationToken ct);
    Task CompletePostFundingEvaluationAsync(
        Guid fundingContinuationId,
        DateTimeOffset now,
        CancellationToken ct);
    Task<IReadOnlyList<Guid>> LeaseFundingContinuationRecoveryBatchAsync(
        string leaseOwner,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct);
}

public interface IPaymentExecutionRepository
{
    Task<PaymentExecutionContext?> GetPaymentExecutionContextAsync(
        byte[] tokenHash,
        CancellationToken ct);
    Task<PaymentExecutionContext?> GetPaymentExecutionContextAsync(
        Guid gatewayPaymentId,
        CancellationToken ct);
    Task<PaymentExecutionContext?> GetPaymentAttemptContextAsync(
        Guid paymentAttemptId,
        CancellationToken ct);
    Task<PaymentExecutionClaimResult> ClaimPaymentExecutionAsync(
        Guid gatewayPaymentId,
        PaymentAttemptTrigger trigger,
        string policyId,
        string policyVersion,
        string policyHash,
        Guid destinationWalletId,
        DateTimeOffset now,
        CancellationToken ct);
    Task<PaymentExecutionClaimResult> ClaimPaymentExecutionAsync(
        Guid gatewayPaymentId,PaymentAttemptTrigger trigger,string policyId,string policyVersion,
        string policyHash,Guid destinationWalletId,
        RiskReservationRequest? risk,
        DateTimeOffset now,
        CancellationToken ct)=>ClaimPaymentExecutionAsync(gatewayPaymentId,trigger,policyId,policyVersion,
            policyHash,destinationWalletId,now,ct);
    Task<PaymentExecutionContext> FinalizePaymentSuccessAsync(
        Guid paymentAttemptId,
        Guid? walletIntentId,
        Guid walletPaymentId,
        DateTimeOffset paidAt,
        DateTimeOffset now,
        CancellationToken ct);
    Task<PaymentExecutionContext> FinalizePaymentInsufficientAsync(
        Guid paymentAttemptId,
        DateTimeOffset now,
        CancellationToken ct);
    Task<PaymentExecutionContext> FinalizePaymentNoEffectAsync(
        Guid paymentAttemptId,
        string outcomeCode,
        DateTimeOffset now,
        CancellationToken ct);
    Task MarkPaymentPendingRecoveryAsync(
        Guid paymentAttemptId,
        DateTimeOffset now,
        DateTimeOffset nextRetryAt,
        CancellationToken ct);
    Task RecordPaymentExecutionAuditAsync(
        Guid gatewayPaymentId,
        Guid? actorId,
        string action,
        DateTimeOffset now,
        CancellationToken ct);
    Task<IReadOnlyList<Guid>> LeasePaymentRecoveryBatchAsync(
        string leaseOwner,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct);
    Task<IReadOnlyList<Guid>> LeaseAutomaticResumeBatchAsync(
        string leaseOwner,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct);
}

public enum PaymentTerminationRequestOutcome
{
    Completed,
    Processing,
    Succeeded,
    AlreadyCanceled,
    AlreadyExpired,
    InvalidState,
    NotFound,
    IdempotencyConflict
}

public sealed record PaymentTerminationRequestResult(
    PaymentTerminationRequestOutcome Outcome,
    GatewayPayment? Payment,
    CheckoutSession? Checkout,
    PaymentTermination? Termination,
    Guid? RecoveryAttemptId = null);

public sealed record ExpirationClaim(Guid GatewayPaymentId, Guid? RecoveryAttemptId);

public interface IPaymentTerminationRepository
{
    Task<PaymentTerminationRequestResult> RequestMerchantCancellationAsync(
        Guid paymentId, Guid merchantId, Guid terminalId, string idempotencyKey,
        byte[] requestHash, string? reason, DateTimeOffset now, CancellationToken ct);
    Task<PaymentTerminationRequestResult> RequestCustomerCancellationAsync(
        Guid checkoutSessionId, Guid gatewayPaymentId, Guid customerId,
        string idempotencyKey, byte[] requestHash, string? reason,
        DateTimeOffset now, CancellationToken ct);
    Task<PaymentExecutionContext?> GetTerminationContextAsync(Guid gatewayPaymentId, CancellationToken ct);
    Task<PaymentExecutionContext> FinalizeTerminationAsync(Guid gatewayPaymentId, DateTimeOffset now, CancellationToken ct);
    Task<PaymentExecutionContext> FinalizeCaptureWonAsync(Guid gatewayPaymentId, Guid paymentAttemptId,
        Guid? walletIntentId, Guid walletPaymentId, DateTimeOffset paidAt, DateTimeOffset now, CancellationToken ct);
    Task MarkTerminationPendingRecoveryAsync(Guid gatewayPaymentId, Guid paymentAttemptId,
        DateTimeOffset now, DateTimeOffset nextRetryAt, CancellationToken ct);
    Task RecordTerminationAuditAsync(Guid gatewayPaymentId, Guid? actorId,
        string action, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<ExpirationClaim>> ClaimDueExpirationsAsync(
        DateTimeOffset now, int batchSize, CancellationToken ct);
}

public enum RefundClaimOutcome { Claimed, Existing, IdempotencyConflict, MerchantReferenceConflict, AmountExceeded, NotFound, InvalidState, IntegrityFailure, RiskDenied }
public sealed record RefundClaimResult(RefundClaimOutcome Outcome, Refund? Refund = null,
    GatewayPayment? Payment = null, string? RiskRuleCode = null);
public sealed record RefundContext(Refund Refund, GatewayPayment Payment);

public interface IRefundRepository
{
    Task<RefundClaimResult> ClaimRefundAsync(Guid paymentId, Guid merchantId, Guid terminalId,
        Guid credentialId, string idempotencyKey, byte[] requestHash, long amountMinor,
        string? merchantReference, string? reason, DateTimeOffset now, CancellationToken ct);
    Task<RefundClaimResult> ClaimRefundAsync(Guid paymentId, Guid merchantId, Guid terminalId,
        Guid credentialId, string idempotencyKey, byte[] requestHash, long amountMinor,
        string? merchantReference, string? reason, RiskReservationRequest? risk,
        DateTimeOffset now, CancellationToken ct)=>ClaimRefundAsync(paymentId,merchantId,terminalId,credentialId,
            idempotencyKey,requestHash,amountMinor,merchantReference,reason,now,ct);
    Task<Refund?> GetRefundAsync(Guid refundId, Guid merchantId, Guid terminalId, CancellationToken ct);
    Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid paymentId, Guid merchantId, Guid terminalId, CancellationToken ct);
    Task<RefundContext?> GetRefundContextAsync(Guid refundId, CancellationToken ct);
    Task<RefundContext> FinalizeRefundSuccessAsync(Guid refundId, Guid walletRefundId,
        DateTimeOffset walletCompletedAt, DateTimeOffset now, CancellationToken ct);
    Task MarkRefundFailedAsync(Guid refundId, string failureCode, DateTimeOffset now, CancellationToken ct);
    Task MarkRefundPendingRecoveryAsync(Guid refundId, DateTimeOffset now, DateTimeOffset nextRetryAt, CancellationToken ct);
    Task<IReadOnlyList<Guid>> LeaseRefundRecoveryBatchAsync(string leaseOwner, DateTimeOffset now,
        DateTimeOffset leaseUntil, int batchSize, CancellationToken ct);
    Task CompleteOrderRefundProjectionAsync(Guid refundId, DateTimeOffset now, CancellationToken ct);
    Task ScheduleOrderRefundProjectionAsync(Guid refundId, DateTimeOffset now, DateTimeOffset nextRetryAt, CancellationToken ct);
    Task RecordRefundAuditAsync(Guid refundId, string action, DateTimeOffset now, CancellationToken ct);
}

public sealed record CheckoutTokenMaterial(
    byte[] Nonce,
    byte[] TokenHash,
    int KeyVersion,
    string Token);

public sealed record CheckoutAuthorizationMaterial(string Secret, byte[] SecretHash);

public interface ICheckoutSecurityService
{
    CheckoutTokenMaterial CreateCheckoutToken(Guid checkoutSessionId, Guid gatewayPaymentId);
    string RecreateCheckoutToken(CheckoutSession checkoutSession);
    bool TryHashPresentedToken(string token, out byte[] tokenHash);
    CheckoutAuthorizationMaterial CreateAuthorizationSecret();
    bool VerifyAuthorizationSecret(string secret, byte[] expectedHash);
}

public interface ICheckoutOtpReferenceProtector
{
    string Protect(string referenceCode);
    bool TryUnprotect(string protectedReferenceCode, out string referenceCode);
}

public interface ICredentialSecretService
{
    (string ClientId, string Secret, string Hash, string Algorithm, int Version) Generate();
    bool Verify(string secret, string hash, string algorithm, int version);
}

public interface IWebhookSecretProtector
{
    (string Secret, string Ciphertext) GenerateAndProtect();
    bool TryUnprotect(string ciphertext, out string secret);
}

public interface IWebhookOperationsRepository
{
    Task<bool> ResendDeadLetterAsync(Guid eventId, string? actorId, string? reason,
        DateTimeOffset now, CancellationToken ct);
}

public sealed record RiskReservationRequest(
    RiskScopeType ScopeType,
    Guid ScopeId,
    string OperationType,
    Guid ResourceId,
    long AmountMinor,
    long? CountLimit,
    long? AmountLimitMinor,
    string CountRuleCode,
    string AmountRuleCode,
    Guid? MerchantId,
    Guid? TerminalId,
    Guid? CustomerId,
    DateTimeOffset BucketStart);

public sealed record ReconciliationObservation(
    string Code,
    ReconciliationSeverity Severity,
    string EvidenceJson,
    string EvidenceHash,
    string? AutoRemediationAction = null);

public sealed record ReconciliationPersistResult(int OpenedOrObserved, int Resolved,
    IReadOnlyDictionary<string, Guid> FindingIds, bool SnapshotCurrent);

public sealed record FundingReconciliationSnapshot(
    Guid ContinuationId, long Version, Guid GatewayPaymentId, Guid MerchantId, Guid TerminalId,
    Guid CustomerId, Guid PersonalWalletId, long AmountMinor, string Currency, string ClientReference,
    Guid? WalletFundingSessionId, FundingContinuationStatus Status, DateTimeOffset UpdatedAt);

public sealed record PaymentReconciliationSnapshot(
    Guid PaymentId, long Version, Guid MerchantId, Guid TerminalId, Guid? CustomerId, Guid? OrderId,
    long AmountMinor, string Currency, GatewayPaymentStatus Status, long CapturedAmountMinor,
    long RefundedAmountMinor, Guid? WalletPaymentId, DateTimeOffset? PaidAt, DateTimeOffset UpdatedAt,
    Guid? ActiveAttemptId, PaymentAttemptStatus? ActiveAttemptStatus);

public sealed record RefundReconciliationSnapshot(
    Guid RefundId, long Version, Guid GatewayPaymentId, Guid MerchantId, Guid TerminalId, Guid? CustomerId,
    Guid? OrderId, Guid? WalletPaymentId, RefundStatus Status, long AmountMinor, string Currency,
    Guid? WalletRefundId, DateTimeOffset CreatedAt, DateTimeOffset? LastCheckedAt,
    OrderRefundProjectionStatus OrderProjectionStatus, DateTimeOffset? OrderProjectionNextRetryAt);
public sealed record ReconciliationBatch<T>(IReadOnlyList<T> Items, string? Cursor);

public interface IReconciliationRepository
{
    Task<ReconciliationRun?> TryStartRunAsync(ReconciliationKind kind, string workerInstanceId,
        DateTimeOffset? from, DateTimeOffset? to, bool dryRun, DateTimeOffset leaseUntil,
        DateTimeOffset now, CancellationToken ct);
    Task CompleteRunAsync(Guid runId, int scanned, int findings, int remediations, int errors,
        string? cursor, DateTimeOffset now, CancellationToken ct);
    Task FailRunAsync(Guid runId, int scanned, int findings, int remediations, int errors,
        string failureCode, DateTimeOffset now, CancellationToken ct);
    Task<ReconciliationBatch<FundingReconciliationSnapshot>> GetFundingCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct);
    Task<ReconciliationBatch<PaymentReconciliationSnapshot>> GetPaymentCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct);
    Task<ReconciliationBatch<RefundReconciliationSnapshot>> GetRefundCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct);
    Task<ReconciliationPersistResult> ReconcileFindingsAsync(Guid runId, ReconciliationKind kind,
        string resourceType, Guid resourceId, long expectedVersion,
        IReadOnlyCollection<ReconciliationObservation> observations,
        ReconciliationResolutionCategory resolutionCategory, DateTimeOffset now, CancellationToken ct);
    Task<bool> TryClaimAutoRemediationAsync(Guid findingId, string action, int maximumAttempts,
        TimeSpan baseDelay, TimeSpan maximumDelay, DateTimeOffset now, CancellationToken ct);
    Task<(int Scanned, int Findings)> AuditInternalIntegrityAsync(Guid runId, DateTimeOffset from, DateTimeOffset to,
        int batchSize, DateTimeOffset now, CancellationToken ct);
    Task<(int Scanned, int Findings)> AuditWebhookIntegrityAsync(Guid runId, DateTimeOffset from, DateTimeOffset to,
        int batchSize, TimeSpan deadLetterStaleAfter, DateTimeOffset now, CancellationToken ct);
    Task<(int Scanned, int Findings, int Repairs)> AuditRiskIntegrityAsync(Guid runId, int batchSize,
        bool allowRepair, DateTimeOffset now, CancellationToken ct);
    Task<bool> AcknowledgeFindingAsync(Guid findingId, string actor, string? note,
        DateTimeOffset now, CancellationToken ct);
}

public interface IWebhookIntegrityRemediator
{
    Task<int> ReconcileMissingEventsAsync(int batchSize, CancellationToken ct);
}
