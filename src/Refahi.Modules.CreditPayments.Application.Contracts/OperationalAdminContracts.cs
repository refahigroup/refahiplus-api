using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed record PagedAdminResult<T>(IReadOnlyList<T> Items,int Page,int PageSize,long TotalCount);

public sealed record AdminPaymentSummaryDto(Guid PaymentId,Guid MerchantId,Guid TerminalId,string MerchantOrderId,
    string Status,long AmountMinor,string Currency,Guid? CustomerId,Guid? OrderId,Guid? WalletPaymentId,
    DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt);
public sealed record AdminAttemptDto(Guid AttemptId,int Sequence,string Trigger,string Status,string? OutcomeCode,
    Guid OrderId,Guid? WalletIntentId,Guid? WalletPaymentId,DateTimeOffset StartedAt,DateTimeOffset? CompletedAt,
    int RetryCount,DateTimeOffset? NextRetryAt);
public sealed record AdminFundingDto(Guid FundingContinuationId,string Status,Guid CustomerId,Guid PersonalWalletId,
    long RequiredDeficitMinor,Guid? WalletFundingSessionId,int RetryCount,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt);
public sealed record AdminTerminationDto(Guid TerminationId,string Kind,string Source,string Status,string? Reason,
    DateTimeOffset RequestedAt,DateTimeOffset? ResolvedAt,int RetryCount);
public sealed record AdminRefundDto(Guid RefundId,Guid PaymentId,Guid TerminalId,string Status,long AmountMinor,string Currency,
    string? MerchantRefundReference,string? FailureCode,Guid? WalletRefundId,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt,int RetryCount);
public sealed record AdminWebhookEventDto(Guid EventId,string EventType,int Version,Guid MerchantId,Guid TerminalId,
    string AggregateType,Guid AggregateId,DateTimeOffset OccurredAt);
public sealed record AdminWebhookDeliveryDto(Guid DeliveryId,Guid EventId,string Status,int AttemptCount,int ManualResendCount,
    DateTimeOffset? NextAttemptAt,DateTimeOffset? LastAttemptAt,DateTimeOffset? DeliveredAt,int? LastHttpStatusCode,string? LastFailureCode);
public sealed record AdminPaymentDetailsDto(AdminPaymentSummaryDto Payment,IReadOnlyList<AdminAttemptDto> Attempts,
    IReadOnlyList<AdminFundingDto> FundingContinuations,AdminTerminationDto? Termination,
    IReadOnlyList<AdminRefundDto> Refunds,IReadOnlyList<AdminWebhookEventDto> WebhookEvents,
    IReadOnlyList<AdminWebhookDeliveryDto> WebhookDeliveries);
public sealed record AdminAuditDto(Guid AuditId,string Action,string EntityType,Guid EntityId,string? ActorId,
    Guid? MerchantId,Guid? TerminalId,string? MerchantOrderId,DateTimeOffset OccurredAt);
public sealed record AdminCredentialMetadataDto(Guid CredentialId,Guid TerminalId,string ClientId,string Label,
    string[] Scopes,string Status,DateTimeOffset CreatedAt,DateTimeOffset? ExpiresAt,DateTimeOffset? LastUsedAt,DateTimeOffset? RevokedAt);
public sealed record AdminOperationalSummaryDto(long ProcessingPayments,long PendingPaymentRecoveries,long AwaitingFunding,
    long PendingRefundRecoveries,long PendingFundingRecoveries,long PendingTerminations,long DeadLetterWebhooks,
    DateTimeOffset? OldestRecoveryAt,long OpenReconciliationFindings=0,long CriticalReconciliationFindings=0,
    DateTimeOffset? OldestReconciliationFindingAt=null,ReconciliationRunDto? LastReconciliationRun=null,
    long UnpublishedIntegrationEvents=0,bool IntegrationPublicationEnabled=false);

public sealed record SearchAdminPaymentsQuery(Guid? PaymentId,Guid? MerchantId,Guid? TerminalId,string? MerchantOrderId,
    string? Status,Guid? CustomerId,DateTimeOffset? From,DateTimeOffset? To,int Page,int PageSize):IRequest<PagedAdminResult<AdminPaymentSummaryDto>>;
public sealed record GetAdminPaymentDetailsQuery(Guid PaymentId):IRequest<AdminPaymentDetailsDto?>;
public sealed record SearchAdminRefundsQuery(Guid? RefundId,Guid? PaymentId,Guid? TerminalId,string? Status,
    DateTimeOffset? From,DateTimeOffset? To,int Page,int PageSize):IRequest<PagedAdminResult<AdminRefundDto>>;
public sealed record GetAdminRefundQuery(Guid RefundId):IRequest<AdminRefundDto?>;
public sealed record SearchAdminAuditQuery(string? EntityType,Guid? EntityId,string? Action,string? Actor,
    DateTimeOffset? From,DateTimeOffset? To,int Page,int PageSize):IRequest<PagedAdminResult<AdminAuditDto>>;
public sealed record GetAdminWebhookEventQuery(Guid EventId):IRequest<AdminWebhookEventDto?>;
public sealed record SearchAdminWebhookDeliveriesQuery(string? Status,Guid? EventId,DateTimeOffset? From,
    DateTimeOffset? To,int Page,int PageSize):IRequest<PagedAdminResult<AdminWebhookDeliveryDto>>;
public sealed record ListAdminCredentialMetadataQuery(Guid TerminalId,int Page,int PageSize):IRequest<PagedAdminResult<AdminCredentialMetadataDto>>;
public sealed record GetAdminOperationalSummaryQuery:IRequest<AdminOperationalSummaryDto>;
public sealed record RequestAdminRecoveryCommand(string ResourceType,Guid ResourceId,string? ActorId):IRequest<string>;

public interface ICreditPaymentsAdminReader
{
    Task<PagedAdminResult<AdminPaymentSummaryDto>> SearchPaymentsAsync(SearchAdminPaymentsQuery query,CancellationToken ct);
    Task<AdminPaymentDetailsDto?> GetPaymentAsync(Guid id,CancellationToken ct);
    Task<PagedAdminResult<AdminRefundDto>> SearchRefundsAsync(SearchAdminRefundsQuery query,CancellationToken ct);
    Task<AdminRefundDto?> GetRefundAsync(Guid id,CancellationToken ct);
    Task<PagedAdminResult<AdminAuditDto>> SearchAuditAsync(SearchAdminAuditQuery query,CancellationToken ct);
    Task<PagedAdminResult<RiskDecisionDto>> SearchRiskAsync(SearchRiskDecisionsQuery query,CancellationToken ct);
    Task<AdminWebhookEventDto?> GetWebhookEventAsync(Guid id,CancellationToken ct);
    Task<PagedAdminResult<AdminWebhookDeliveryDto>> SearchWebhookDeliveriesAsync(SearchAdminWebhookDeliveriesQuery query,CancellationToken ct);
    Task<PagedAdminResult<AdminCredentialMetadataDto>> ListCredentialsAsync(ListAdminCredentialMetadataQuery query,CancellationToken ct);
    Task<AdminOperationalSummaryDto> GetSummaryAsync(CancellationToken ct);
    Task<AdminRecoveryTarget?> GetRecoveryTargetAsync(string resourceType,Guid resourceId,CancellationToken ct);
    Task AuditRecoveryRequestAsync(string resourceType,Guid resourceId,string? actorId,DateTimeOffset now,CancellationToken ct);
}

public sealed record AdminRecoveryTarget(string ResourceType,Guid ResourceId,Guid? PaymentAttemptId,string Status);
