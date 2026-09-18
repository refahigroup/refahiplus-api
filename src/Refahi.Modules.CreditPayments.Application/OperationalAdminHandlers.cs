using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class OperationalAdminQueryHandlers(ICreditPaymentsAdminReader reader):
    IRequestHandler<SearchAdminMerchantsQuery,PagedAdminResult<MerchantDto>>,
    IRequestHandler<SearchAdminTerminalsQuery,PagedAdminResult<TerminalDto>>,
    IRequestHandler<SearchAdminPaymentsQuery,PagedAdminResult<AdminPaymentSummaryDto>>,
    IRequestHandler<GetAdminPaymentDetailsQuery,AdminPaymentDetailsDto?>,
    IRequestHandler<SearchAdminRefundsQuery,PagedAdminResult<AdminRefundDto>>,
    IRequestHandler<GetAdminRefundQuery,AdminRefundDto?>,
    IRequestHandler<SearchAdminAuditQuery,PagedAdminResult<AdminAuditDto>>,
    IRequestHandler<SearchRiskDecisionsQuery,PagedAdminResult<RiskDecisionDto>>,
    IRequestHandler<GetAdminWebhookEventQuery,AdminWebhookEventDto?>,
    IRequestHandler<SearchAdminWebhookDeliveriesQuery,PagedAdminResult<AdminWebhookDeliveryDto>>,
    IRequestHandler<SearchAdminIntegrationEventsQuery,PagedAdminResult<AdminIntegrationEventDto>>,
    IRequestHandler<ListAdminCredentialMetadataQuery,PagedAdminResult<AdminCredentialMetadataDto>>,
    IRequestHandler<GetAdminOperationalSummaryQuery,AdminOperationalSummaryDto>
{
    public Task<PagedAdminResult<MerchantDto>> Handle(SearchAdminMerchantsQuery r,CancellationToken ct)=>reader.SearchMerchantsAsync(r,ct);
    public Task<PagedAdminResult<TerminalDto>> Handle(SearchAdminTerminalsQuery r,CancellationToken ct)=>reader.SearchTerminalsAsync(r,ct);
    public Task<PagedAdminResult<AdminPaymentSummaryDto>> Handle(SearchAdminPaymentsQuery r,CancellationToken ct)=>reader.SearchPaymentsAsync(r,ct);
    public Task<AdminPaymentDetailsDto?> Handle(GetAdminPaymentDetailsQuery r,CancellationToken ct)=>reader.GetPaymentAsync(r.PaymentId,ct);
    public Task<PagedAdminResult<AdminRefundDto>> Handle(SearchAdminRefundsQuery r,CancellationToken ct)=>reader.SearchRefundsAsync(r,ct);
    public Task<AdminRefundDto?> Handle(GetAdminRefundQuery r,CancellationToken ct)=>reader.GetRefundAsync(r.RefundId,ct);
    public Task<PagedAdminResult<AdminAuditDto>> Handle(SearchAdminAuditQuery r,CancellationToken ct)=>reader.SearchAuditAsync(r,ct);
    public Task<PagedAdminResult<RiskDecisionDto>> Handle(SearchRiskDecisionsQuery r,CancellationToken ct)=>reader.SearchRiskAsync(r,ct);
    public Task<AdminWebhookEventDto?> Handle(GetAdminWebhookEventQuery r,CancellationToken ct)=>reader.GetWebhookEventAsync(r.EventId,ct);
    public Task<PagedAdminResult<AdminWebhookDeliveryDto>> Handle(SearchAdminWebhookDeliveriesQuery r,CancellationToken ct)=>reader.SearchWebhookDeliveriesAsync(r,ct);
    public Task<PagedAdminResult<AdminIntegrationEventDto>> Handle(SearchAdminIntegrationEventsQuery r,CancellationToken ct)=>reader.SearchIntegrationEventsAsync(r,ct);
    public Task<PagedAdminResult<AdminCredentialMetadataDto>> Handle(ListAdminCredentialMetadataQuery r,CancellationToken ct)=>reader.ListCredentialsAsync(r,ct);
    public Task<AdminOperationalSummaryDto> Handle(GetAdminOperationalSummaryQuery r,CancellationToken ct)=>reader.GetSummaryAsync(ct);
}

public sealed class RequestAdminRecoveryHandler(ICreditPaymentsAdminReader reader,IMediator mediator,TimeProvider clock):
    IRequestHandler<RequestAdminRecoveryCommand,string>
{
    public async Task<string> Handle(RequestAdminRecoveryCommand request,CancellationToken ct)
    {
        var type=request.ResourceType.Trim().ToLowerInvariant();
        var target=await reader.GetRecoveryTargetAsync(type,request.ResourceId,ct)
            ??throw new CreditPaymentsApplicationException("recovery_not_required","منبع موردنظر نیازمند بازیابی نیست",409);
        await reader.AuditRecoveryRequestAsync(type,request.ResourceId,request.ActorId,clock.GetUtcNow(),ct);
        switch(type)
        {
            case "payment": case "termination":
                if(target.PaymentAttemptId is null)throw new CreditPaymentsApplicationException("recovery_not_required","پرداخت نیازمند بازیابی نیست",409);
                await mediator.Send(new RecoverGatewayPaymentCommand(target.PaymentAttemptId.Value),ct);break;
            case "refund": await mediator.Send(new ProcessRefundCommand(request.ResourceId),ct);break;
            case "funding": await mediator.Send(new RecoverFundingContinuationCommand(request.ResourceId),ct);break;
            default: throw new CreditPaymentsApplicationException("recovery_type_invalid","نوع بازیابی نامعتبر است",400);
        }
        return "requested";
    }
}

public sealed class GetCreditPaymentsProductionReadinessHandler(
    ICreditPaymentsProductionReadinessChecker checker)
    : IRequestHandler<GetCreditPaymentsProductionReadinessQuery,CreditPaymentsProductionReadinessDto>
{
    public Task<CreditPaymentsProductionReadinessDto> Handle(
        GetCreditPaymentsProductionReadinessQuery request,
        CancellationToken cancellationToken) => checker.CheckAsync(cancellationToken);
}

public sealed class GetCreditPaymentsGovernanceStatusHandler(
    IOptions<MerchantPaymentPolicyOptions> policies,
    IOptions<RiskOptions> risk,
    IOptions<ReconciliationOptions> reconciliation,
    IOptions<WebhookOptions> webhooks,
    IOptions<SettlementIntegrationOptions> settlement,
    IOptions<CreditPaymentsProductionReadinessOptions> readiness)
    : IRequestHandler<GetCreditPaymentsGovernanceStatusQuery,CreditPaymentsGovernanceStatusDto>
{
    public Task<CreditPaymentsGovernanceStatusDto> Handle(GetCreditPaymentsGovernanceStatusQuery request,CancellationToken ct)
    {
        var riskConfigured = risk.Value.TerminalPayments.Enabled || risk.Value.CustomerPayments.Enabled
            || risk.Value.TerminalRefunds.Enabled || risk.Value.CustomerFundingStarts.Enabled
            || risk.Value.TerminalOverrides.Count > 0;
        var webhookConfigured = webhooks.Value.AllowedPorts.Length > 0 && webhooks.Value.MaximumAttempts > 0;
        return Task.FromResult(new CreditPaymentsGovernanceStatusDto(
            !string.IsNullOrWhiteSpace(policies.Value.FinanceApprovalReference),policies.Value.Policies.Count > 0,
            riskConfigured,reconciliation.Value.Enabled,reconciliation.Value.AutoRemediationEnabled,
            webhookConfigured,settlement.Value.PublicationEnabled,readiness.Value.SettlementLaunchMode,
            readiness.Value.HostedCheckoutRequired,readiness.Value.FinancialExecutionEnabled));
    }
}
