using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class RefundEndpoints:IEndpoint
{
    public void Map(object app)
    {
        if(app is not IEndpointRouteBuilder r)return;
        r.MapPost("/payments/{paymentId:guid}/refunds",Create).WithName("CreditPayments.CreateRefund")
            .WithTags("CreditPayments.Refunds").RequireAuthorization(DI.RefundCreatePolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy).Produces<ApiResponse<RefundDto>>(202);
        r.MapGet("/payments/{paymentId:guid}/refunds",List).WithName("CreditPayments.ListRefunds")
            .WithTags("CreditPayments.Refunds").RequireAuthorization(DI.RefundReadPolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy).Produces<ApiResponse<IReadOnlyList<RefundDto>>>(200);
        r.MapGet("/refunds/{refundId:guid}",Get).WithName("CreditPayments.GetRefund")
            .WithTags("CreditPayments.Refunds").RequireAuthorization(DI.RefundReadPolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy).Produces<ApiResponse<RefundDto>>(200);
    }
    private static async Task<IResult>Create(Guid paymentId,[FromHeader(Name="Idempotency-Key")]string? key,
        [FromBody]CreateRefundRequest body,ClaimsPrincipal user,IMediator mediator,CancellationToken ct)
    {var c=Context(user);var x=await mediator.Send(new CreateRefundCommand(c.Merchant,c.Terminal,c.Credential,paymentId,key??"",body.AmountMinor,body.MerchantRefundReference,body.Reason),ct);return Results.Accepted($"refunds/{x.RefundId:D}",ApiResponseHelper.Success(x,"درخواست بازگشت وجه پذیرفته شد",202));}
    private static async Task<IResult>Get(Guid refundId,ClaimsPrincipal user,IMediator mediator,CancellationToken ct)
    {var c=Context(user);var x=await mediator.Send(new GetRefundQuery(c.Merchant,c.Terminal,refundId),ct);return x is null?NotFound("refund_not_found","بازگشت وجه یافت نشد"):Results.Ok(ApiResponseHelper.Success(x,"اطلاعات بازگشت وجه دریافت شد"));}
    private static async Task<IResult>List(Guid paymentId,ClaimsPrincipal user,IMediator mediator,CancellationToken ct)
    {var c=Context(user);var x=await mediator.Send(new ListRefundsQuery(c.Merchant,c.Terminal,paymentId),ct);return x is null?NotFound("payment_not_found","پرداخت یافت نشد"):Results.Ok(ApiResponseHelper.Success(x,"فهرست بازگشت وجه دریافت شد"));}
    private static (Guid Merchant,Guid Terminal,Guid Credential)Context(ClaimsPrincipal p)=>(Guid.Parse(p.FindFirstValue("merchant_id")!),Guid.Parse(p.FindFirstValue("terminal_id")!),Guid.Parse(p.FindFirstValue("credential_id")!));
    private static IResult NotFound(string code,string message)=>Results.NotFound(ApiResponseHelper.Error(message,new Dictionary<string,string[]>{{"code",[code]}},404));
}
public sealed record CreateRefundRequest(long AmountMinor,string? MerchantRefundReference,string? Reason);
