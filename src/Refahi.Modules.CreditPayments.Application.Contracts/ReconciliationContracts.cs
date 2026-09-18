using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class ReconciliationOptions
{
    public const string SectionName = "CreditPayments:Reconciliation";
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 300;
    public int LookbackHours { get; set; } = 24;
    public int MaximumHistoricalRangeDays { get; set; } = 90;
    public int ProcessingStaleAfterMinutes { get; set; } = 30;
    public int FundingStaleAfterMinutes { get; set; } = 30;
    public int RefundStaleAfterMinutes { get; set; } = 30;
    public int ProjectionStaleAfterMinutes { get; set; } = 30;
    public int DeadLetterStaleAfterMinutes { get; set; } = 60;
    public bool AutoRemediationEnabled { get; set; }
    public int MaxAutoRemediationAttempts { get; set; } = 3;
    public int BaseRetrySeconds { get; set; } = 60;
    public int MaximumRetrySeconds { get; set; } = 3600;
}

public sealed record ReconciliationRunDto(Guid RunId, string Kind, string Status,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string WorkerInstanceId, bool DryRun,
    int ScannedCount, int FindingCount, int AutoRemediationCount, int ErrorCount, string? FailureCode,
    DateTimeOffset? LeaseUntil = null);

public sealed record ReconciliationFindingDto(Guid FindingId, Guid? RunId, string Kind, string FindingCode,
    string Severity, string ResourceType, Guid ResourceId, Guid? MerchantId, Guid? TerminalId, Guid? CustomerId,
    string Status, DateTimeOffset FirstObservedAt, DateTimeOffset LastObservedAt, int ObservationCount,
    DateTimeOffset? ResolvedAt, string? ResolutionCategory, DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy, string? AcknowledgementNote, string? AutoRemediationAction,
    int AutoRemediationAttemptCount, DateTimeOffset? NextAutoRemediationAt, string EvidenceJson);

public sealed record StartReconciliationRunCommand(string Kind, DateTimeOffset? From,
    DateTimeOffset? To, bool DryRun, string? WorkerInstanceId = null) : IRequest<ReconciliationRunDto?>;
public sealed record ExecuteReconciliationRunCommand(short Kind, DateTimeOffset? From,
    DateTimeOffset? To, bool DryRun, string WorkerInstanceId) : IRequest<ReconciliationRunDto?>;
public sealed record SearchReconciliationRunsQuery(string? Kind, string? Status,
    DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize)
    : IRequest<PagedAdminResult<ReconciliationRunDto>>;
public sealed record GetReconciliationRunQuery(Guid RunId) : IRequest<ReconciliationRunDto?>;
public sealed record SearchReconciliationFindingsQuery(string? Code, string? Severity, string? Status,
    string? ResourceType, Guid? ResourceId, Guid? MerchantId, Guid? TerminalId,
    DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize)
    : IRequest<PagedAdminResult<ReconciliationFindingDto>>;
public sealed record GetReconciliationFindingQuery(Guid FindingId) : IRequest<ReconciliationFindingDto?>;
public sealed record AcknowledgeReconciliationFindingCommand(Guid FindingId, string ActorId, string? Note)
    : IRequest<bool>;
public sealed record ReconcileMissingWebhookEventsCommand(int BatchSize) : IRequest<int>;
public sealed record ReconcileMissingCreditPaymentIntegrationEventsCommand(int BatchSize) : IRequest<int>;

public interface IReconciliationAdminReader
{
    Task<PagedAdminResult<ReconciliationRunDto>> SearchRunsAsync(SearchReconciliationRunsQuery query, CancellationToken ct);
    Task<ReconciliationRunDto?> GetRunAsync(Guid id, CancellationToken ct);
    Task<PagedAdminResult<ReconciliationFindingDto>> SearchFindingsAsync(SearchReconciliationFindingsQuery query, CancellationToken ct);
    Task<ReconciliationFindingDto?> GetFindingAsync(Guid id, CancellationToken ct);
}
