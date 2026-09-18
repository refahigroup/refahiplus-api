using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class CreditPaymentsDataProtectionOptions
{
    public const string SectionName = "CreditPayments:DataProtection";
    public const string SharedApplicationName = "Refahi.CreditPayments";
    public bool RequirePersistentKeyRing { get; set; }
    public string ApplicationName { get; set; } = SharedApplicationName;
    public string KeyRingPath { get; set; } = string.Empty;
    public string ProtectionCertificatePath { get; set; } = string.Empty;
    public string ProtectionCertificatePassword { get; set; } = string.Empty;
}

public sealed class CreditPaymentsProductionReadinessOptions
{
    public const string SectionName = "CreditPayments:ProductionReadiness";
    public bool FinancialExecutionEnabled { get; set; } = true;
    public bool HostedCheckoutRequired { get; set; } = true;
    public string HostedCheckoutDeploymentEvidence { get; set; } = string.Empty;
    public string SettlementLaunchMode { get; set; } = string.Empty;
}

public sealed record ProductionReadinessRequirementDto(
    string Requirement,
    string Status,
    bool ProductionBlocker,
    string Evidence);

public sealed record CreditPaymentsProductionReadinessDto(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<ProductionReadinessRequirementDto> Requirements);

public sealed record GetCreditPaymentsProductionReadinessQuery
    : IRequest<CreditPaymentsProductionReadinessDto>;

public sealed record CreditPaymentsGovernanceStatusDto(
    bool FinanceApprovalConfigured,
    bool MerchantPaymentPolicyConfigured,
    bool RiskConfigured,
    bool ReconciliationEnabled,
    bool AutoRemediationEnabled,
    bool WebhookDeliveryEnabled,
    bool IntegrationPublicationEnabled,
    string SettlementLaunchMode,
    bool HostedCheckoutRequired,
    bool FinancialExecutionEnabled);

public sealed record GetCreditPaymentsGovernanceStatusQuery
    : IRequest<CreditPaymentsGovernanceStatusDto>;

public interface ICreditPaymentsProductionReadinessChecker
{
    Task<CreditPaymentsProductionReadinessDto> CheckAsync(CancellationToken cancellationToken);
}
