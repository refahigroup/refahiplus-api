using MediatR;
using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class OperationalAdminQueryHandlers(ICreditPaymentsAdminReader reader):
    IRequestHandler<SearchAdminPaymentsQuery,PagedAdminResult<AdminPaymentSummaryDto>>,
    IRequestHandler<GetAdminPaymentDetailsQuery,AdminPaymentDetailsDto?>,
    IRequestHandler<SearchAdminRefundsQuery,PagedAdminResult<AdminRefundDto>>,
    IRequestHandler<GetAdminRefundQuery,AdminRefundDto?>,
    IRequestHandler<SearchAdminAuditQuery,PagedAdminResult<AdminAuditDto>>,
    IRequestHandler<SearchRiskDecisionsQuery,PagedAdminResult<RiskDecisionDto>>,
    IRequestHandler<GetAdminWebhookEventQuery,AdminWebhookEventDto?>,
    IRequestHandler<SearchAdminWebhookDeliveriesQuery,PagedAdminResult<AdminWebhookDeliveryDto>>,
    IRequestHandler<ListAdminCredentialMetadataQuery,PagedAdminResult<AdminCredentialMetadataDto>>,
    IRequestHandler<GetAdminOperationalSummaryQuery,AdminOperationalSummaryDto>
{
    public Task<PagedAdminResult<AdminPaymentSummaryDto>> Handle(SearchAdminPaymentsQuery r,CancellationToken ct)=>reader.SearchPaymentsAsync(r,ct);
    public Task<AdminPaymentDetailsDto?> Handle(GetAdminPaymentDetailsQuery r,CancellationToken ct)=>reader.GetPaymentAsync(r.PaymentId,ct);
    public Task<PagedAdminResult<AdminRefundDto>> Handle(SearchAdminRefundsQuery r,CancellationToken ct)=>reader.SearchRefundsAsync(r,ct);
    public Task<AdminRefundDto?> Handle(GetAdminRefundQuery r,CancellationToken ct)=>reader.GetRefundAsync(r.RefundId,ct);
    public Task<PagedAdminResult<AdminAuditDto>> Handle(SearchAdminAuditQuery r,CancellationToken ct)=>reader.SearchAuditAsync(r,ct);
    public Task<PagedAdminResult<RiskDecisionDto>> Handle(SearchRiskDecisionsQuery r,CancellationToken ct)=>reader.SearchRiskAsync(r,ct);
    public Task<AdminWebhookEventDto?> Handle(GetAdminWebhookEventQuery r,CancellationToken ct)=>reader.GetWebhookEventAsync(r.EventId,ct);
    public Task<PagedAdminResult<AdminWebhookDeliveryDto>> Handle(SearchAdminWebhookDeliveriesQuery r,CancellationToken ct)=>reader.SearchWebhookDeliveriesAsync(r,ct);
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
