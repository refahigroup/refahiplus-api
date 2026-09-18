using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class RiskOptions
{
    public const string SectionName = "CreditPayments:Risk";
    public string BucketTimeZone { get; set; } = "UTC";
    public RiskLimitOptions TerminalPayments { get; set; } = new();
    public RiskLimitOptions CustomerPayments { get; set; } = new();
    public RiskLimitOptions TerminalRefunds { get; set; } = new();
    public RiskLimitOptions CustomerFundingStarts { get; set; } = new();
    public List<TerminalRiskOverride> TerminalOverrides { get; set; } = [];
}

public sealed class RiskLimitOptions
{
    public bool Enabled { get; set; }
    public long? DailyCountLimit { get; set; }
    public long? DailyAmountLimitMinor { get; set; }
}

public sealed class TerminalRiskOverride
{
    public Guid TerminalId { get; set; }
    public RiskLimitOptions? Payments { get; set; }
    public RiskLimitOptions? Refunds { get; set; }
}

public sealed record RiskDecisionDto(Guid DecisionId, string OperationType, Guid? MerchantId,
    Guid? TerminalId, Guid? CustomerId, Guid ResourceId, string RuleCode, string Decision,
    long? CurrentValue, long? LimitValue, DateTimeOffset OccurredAt, string? CorrelationId);

public sealed record SearchRiskDecisionsQuery(Guid? ResourceId, string? RuleCode, string? OperationType,
    string? Decision, Guid? MerchantId, Guid? TerminalId, Guid? CustomerId,
    DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize) : IRequest<PagedAdminResult<RiskDecisionDto>>;
