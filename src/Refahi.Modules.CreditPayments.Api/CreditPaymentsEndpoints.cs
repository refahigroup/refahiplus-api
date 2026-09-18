using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class CreditPaymentsAdminEndpoints:IEndpoint
{
    public void Map(object app)
    {
        if(app is not IEndpointRouteBuilder r)return;
        r.MapPost("/admin/merchants",async([FromBody]CreateMerchantCommand x,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Created($"merchants/{x.SupplierId}",ApiResponseHelper.Success(await m.Send(x with { ActorId=Actor(user) },ct),"پذیرنده ایجاد شد"))).WithName("CreditPayments.CreateMerchant").WithTags("CreditPayments.Admin.Merchants").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/merchants/{id:guid}",async(Guid id,IMediator m,CancellationToken ct)=>await m.Send(new GetMerchantQuery(id),ct) is { } x?Results.Ok(ApiResponseHelper.Success(x)):Results.NotFound(ApiResponseHelper.Error("پذیرنده یافت نشد",statusCode:404))).WithName("CreditPayments.GetMerchant").WithTags("CreditPayments.Admin.Merchants").RequireAuthorization("AdminOnly");
        MapMerchantAction(r,"activate");MapMerchantAction(r,"suspend");MapMerchantAction(r,"disable");
        r.MapPost("/admin/merchants/{id:guid}/terminals",async(Guid id,[FromBody]CreateTerminalRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Created($"terminals/{id}",ApiResponseHelper.Success(await m.Send(new CreateTerminalCommand(id,b.Name,b.ApprovedCategoryCode,b.MinPaymentAmountMinor,b.MaxPaymentAmountMinor,b.AllowedReturnUrls,Actor(user)),ct),"پایانه ایجاد شد"))).WithName("CreditPayments.CreateTerminal").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/terminals/{id:guid}",async(Guid id,IMediator m,CancellationToken ct)=>await m.Send(new GetTerminalQuery(id),ct) is { } x?Results.Ok(ApiResponseHelper.Success(x)):Results.NotFound(ApiResponseHelper.Error("پایانه یافت نشد",statusCode:404))).WithName("CreditPayments.GetTerminal").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        r.MapPut("/admin/terminals/{id:guid}/policy",async(Guid id,[FromBody]UpdateTerminalPolicyRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new UpdateTerminalPolicyCommand(id,b.ApprovedCategoryCode,b.MinPaymentAmountMinor,b.MaxPaymentAmountMinor,b.ExpectedVersion,Actor(user)),ct),"سیاست پایانه به‌روزرسانی شد"))).WithName("CreditPayments.UpdateTerminalPolicy").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        r.MapPost("/admin/terminals/{id:guid}/return-urls",async(Guid id,[FromBody]ReturnUrlRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new AddReturnUrlCommand(id,b.Url,b.ExpectedVersion,Actor(user)),ct),"نشانی بازگشت افزوده شد"))).WithName("CreditPayments.AddReturnUrl").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        r.MapDelete("/admin/terminals/{id:guid}/return-urls/{returnUrlId:guid}",async(Guid id,Guid returnUrlId,[FromQuery]long expectedVersion,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new RemoveReturnUrlCommand(id,returnUrlId,expectedVersion,Actor(user)),ct),"نشانی بازگشت حذف شد"))).WithName("CreditPayments.RemoveReturnUrl").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
        MapTerminalAction(r,"activate");MapTerminalAction(r,"suspend");MapTerminalAction(r,"disable");
        r.MapPost("/admin/terminals/{id:guid}/credentials",async(Guid id,[FromBody]IssueCredentialRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new IssueCredentialCommand(id,b.Label,b.Scopes,b.ExpiresAt,Actor(user)),ct),"اعتبارنامه صادر شد؛ راز فقط همین یک بار نمایش داده می‌شود"))).WithName("CreditPayments.IssueCredential").WithTags("CreditPayments.Admin.Credentials").RequireAuthorization("AdminOnly");
        r.MapPost("/admin/credentials/{id:guid}/revoke",async(Guid id,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>{await m.Send(new RevokeCredentialCommand(id,Actor(user)),ct);return Results.Ok(ApiResponseHelper.Success<object>(null!,"اعتبارنامه لغو شد"));}).WithName("CreditPayments.RevokeCredential").WithTags("CreditPayments.Admin.Credentials").RequireAuthorization("AdminOnly");
        r.MapPut("/admin/terminals/{id:guid}/webhook",async(Guid id,[FromBody]ConfigureWebhookRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new ConfigureWebhookCommand(id,b.Url,b.ExpectedVersion,Actor(user)),ct),"وب‌هوک پیکربندی شد؛ راز فقط همین یک بار نمایش داده می‌شود"))).WithName("CreditPayments.ConfigureWebhook").WithTags("CreditPayments.Admin.Webhooks").RequireAuthorization("AdminOnly");
        r.MapPost("/admin/terminals/{id:guid}/webhook/disable",async(Guid id,[FromBody]VersionRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new DisableWebhookCommand(id,b.ExpectedVersion,Actor(user)),ct),"وب‌هوک غیرفعال شد"))).WithName("CreditPayments.DisableWebhook").WithTags("CreditPayments.Admin.Webhooks").RequireAuthorization("AdminOnly");
    }
    private static void MapMerchantAction(IEndpointRouteBuilder r,string action)=>r.MapPost($"/admin/merchants/{{id:guid}}/{action}",async(Guid id,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new ChangeMerchantStatusCommand(id,action,Actor(user)),ct),"وضعیت پذیرنده تغییر کرد"))).WithName($"CreditPayments.Merchant.{action}").WithTags("CreditPayments.Admin.Merchants").RequireAuthorization("AdminOnly");
    private static void MapTerminalAction(IEndpointRouteBuilder r,string action)=>r.MapPost($"/admin/terminals/{{id:guid}}/{action}",async(Guid id,[FromBody]VersionRequest b,ClaimsPrincipal user,IMediator m,CancellationToken ct)=>Results.Ok(ApiResponseHelper.Success(await m.Send(new ChangeTerminalStatusCommand(id,action,b.ExpectedVersion,Actor(user)),ct),"وضعیت پایانه تغییر کرد"))).WithName($"CreditPayments.Terminal.{action}").WithTags("CreditPayments.Admin.Terminals").RequireAuthorization("AdminOnly");
    private static string? Actor(ClaimsPrincipal user)=>user.FindFirstValue(ClaimTypes.NameIdentifier)??user.FindFirstValue("sub");
}
public sealed record CreateTerminalRequest(string Name,string ApprovedCategoryCode,long MinPaymentAmountMinor,long MaxPaymentAmountMinor,IReadOnlyList<string> AllowedReturnUrls);
public sealed record UpdateTerminalPolicyRequest(string ApprovedCategoryCode,long MinPaymentAmountMinor,long MaxPaymentAmountMinor,long ExpectedVersion);
public sealed record ReturnUrlRequest(string Url,long ExpectedVersion);
public sealed record VersionRequest(long ExpectedVersion);
public sealed record IssueCredentialRequest(string Label,IReadOnlyList<string> Scopes,DateTimeOffset? ExpiresAt);
public sealed record ConfigureWebhookRequest(string Url,long ExpectedVersion);
