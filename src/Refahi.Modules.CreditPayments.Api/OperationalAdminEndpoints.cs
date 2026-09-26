using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class OperationalAdminEndpoints:IEndpoint
{
    public void Map(object app)
    {
        if(app is not IEndpointRouteBuilder r)return;
        r.MapGet("/admin/merchants",SearchMerchants).WithName("CreditPayments.Admin.SearchMerchants").WithTags("CreditPayments.Admin.Merchants").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/terminals",SearchTerminals).WithName("CreditPayments.Admin.SearchTerminals").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/payments",SearchPayments).WithName("CreditPayments.Admin.SearchPayments").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/payments/{paymentId:guid}",GetPayment).WithName("CreditPayments.Admin.GetPayment").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/refunds",SearchRefunds).WithName("CreditPayments.Admin.SearchRefunds").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/refunds/{refundId:guid}",GetRefund).WithName("CreditPayments.Admin.GetRefund").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/audit",SearchAudit).WithName("CreditPayments.Admin.SearchAudit").WithTags("CreditPayments.Admin.Audit").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/risk-decisions",SearchRisk).WithName("CreditPayments.Admin.SearchRiskDecisions").WithTags("CreditPayments.Admin.Risk").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/webhook-events/{eventId:guid}",GetWebhookEvent).WithName("CreditPayments.Admin.GetWebhookEvent").WithTags("CreditPayments.Admin.Webhooks").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/webhook-deliveries",SearchDeliveries).WithName("CreditPayments.Admin.SearchWebhookDeliveries").WithTags("CreditPayments.Admin.Webhooks").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/integration-events",SearchIntegrationEvents).WithName("CreditPayments.Admin.SearchIntegrationEvents").WithTags("CreditPayments.Admin.IntegrationEvents").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/terminals/{terminalId:guid}/credentials",ListCredentials).WithName("CreditPayments.Admin.ListCredentials").WithTags("CreditPayments.Admin.Credentials").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/operations/summary",Summary).WithName("CreditPayments.Admin.OperationalSummary").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/operations/recovery-queue",RecoveryQueue).WithName("CreditPayments.Admin.RecoveryQueue").WithTags("CreditPayments.Admin.Recovery").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/production-readiness",ProductionReadiness).WithName("CreditPayments.Admin.ProductionReadiness").WithTags("CreditPayments.Admin.Operations").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/governance-status",GovernanceStatus).WithName("CreditPayments.Admin.GovernanceStatus").WithTags("CreditPayments.Admin.Governance").RequireAuthorization("AdminOnly");
        MapRecovery(r,"payments","payment");MapRecovery(r,"refunds","refund");MapRecovery(r,"funding-continuations","funding");MapRecovery(r,"terminations","termination");
    }

    private static async Task<IResult> SearchMerchants(Guid? merchantId,string? nationalId,string? status,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminMerchantsQuery(merchantId,nationalId,status,page??1,pageSize??50),ct));
    private static async Task<IResult> SearchTerminals(Guid? terminalId,Guid? merchantId,string? status,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminTerminalsQuery(terminalId,merchantId,status,page??1,pageSize??50),ct));
    private static async Task<IResult> SearchPayments(Guid? paymentId,Guid? merchantId,Guid? terminalId,string? merchantOrderId,string? status,Guid? customerId,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminPaymentsQuery(paymentId,merchantId,terminalId,merchantOrderId,status,customerId,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> GetPayment(Guid paymentId,IMediator m,CancellationToken ct)=>await m.Send(new GetAdminPaymentDetailsQuery(paymentId),ct) is { } x?Results.Ok(ApiResponseHelper.Success(x)):Results.NotFound(ApiResponseHelper.Error("پرداخت یافت نشد",statusCode:404));
    private static async Task<IResult> SearchRefunds(Guid? refundId,Guid? paymentId,Guid? terminalId,Guid? merchantId,string? status,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminRefundsQuery(refundId,paymentId,terminalId,merchantId,status,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> GetRefund(Guid refundId,IMediator m,CancellationToken ct)=>await m.Send(new GetAdminRefundQuery(refundId),ct) is { } x?Results.Ok(ApiResponseHelper.Success(x)):Results.NotFound(ApiResponseHelper.Error("بازگشت وجه یافت نشد",statusCode:404));
    private static async Task<IResult> SearchAudit(string? entityType,Guid? entityId,string? action,string? actor,Guid? merchantId,Guid? terminalId,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminAuditQuery(entityType,entityId,action,actor,merchantId,terminalId,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> SearchRisk(Guid? resourceId,string? ruleCode,string? operationType,string? decision,Guid? merchantId,Guid? terminalId,Guid? customerId,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchRiskDecisionsQuery(resourceId,ruleCode,operationType,decision,merchantId,terminalId,customerId,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> GetWebhookEvent(Guid eventId,IMediator m,CancellationToken ct)=>await m.Send(new GetAdminWebhookEventQuery(eventId),ct) is { } x?Results.Ok(ApiResponseHelper.Success(x)):Results.NotFound(ApiResponseHelper.Error("رویداد وب‌هوک یافت نشد",statusCode:404));
    private static async Task<IResult> SearchDeliveries(string? status,Guid? eventId,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminWebhookDeliveriesQuery(status,eventId,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> SearchIntegrationEvents(Guid? eventId,string? status,string? eventType,Guid? aggregateId,DateTimeOffset? from,DateTimeOffset? to,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminIntegrationEventsQuery(eventId,status,eventType,aggregateId,from,to,page??1,pageSize??50),ct));
    private static async Task<IResult> ListCredentials(Guid terminalId,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new ListAdminCredentialMetadataQuery(terminalId,page??1,pageSize??50),ct));
    private static async Task<IResult> Summary(IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new GetAdminOperationalSummaryQuery(),ct)));
    private static async Task<IResult> RecoveryQueue(string? resourceType,int? page,int? pageSize,IMediator m,CancellationToken ct)=>Paged(await m.Send(new SearchAdminRecoveryQueueQuery(resourceType??"payment",page??1,pageSize??50),ct));
    private static async Task<IResult> ProductionReadiness(IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCreditPaymentsProductionReadinessQuery(),ct)));
    private static async Task<IResult> GovernanceStatus(IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCreditPaymentsGovernanceStatusQuery(),ct)));
    private static void MapRecovery(IEndpointRouteBuilder r,string route,string type)=>r.MapPost($"/admin/{route}/{{id:guid}}/recover",async(Guid id,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Accepted(value:ApiResponseHelper.Success(await m.Send(new RequestAdminRecoveryCommand(type,id,Actor(user)),ct),"درخواست بازیابی ثبت شد",202))).WithName($"CreditPayments.Admin.Recover.{type}").WithTags("CreditPayments.Admin.Recovery").RequireAuthorization("AdminOnly");
    private static string? Actor(ClaimsPrincipal user)=>user.FindFirstValue(ClaimTypes.NameIdentifier)??user.FindFirstValue("sub");
    private static IResult Paged<T>(PagedAdminResult<T> x)=>Results.Ok(ApiResponseHelper.SuccessPaginated(x.Items,x.Page,x.PageSize,(int)Math.Min(int.MaxValue,x.TotalCount)));
}
