using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Promotions.Application.Contracts;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Promotions.Api;

public sealed record GenerateCodesBody(string? PublicCode, Guid? UserId, int Count, int MaximumUses, string IdempotencyKey);
public sealed record ActiveCampaignBody(bool Active);
public sealed record ExportCodesBody(string BatchKey);
public sealed class PromotionEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        var g = routes.MapGroup("/admin").RequireAuthorization("AdminOnly").WithTags("Promotions.Admin");
        g.MapGet("/funding", async (ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new Refahi.Modules.Orders.Application.Contracts.Queries.GetPromotionFundingStatusQuery(), ct)))).WithName("Promotions.GetFundingStatus");
        g.MapGet("/campaigns", async (string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ListCampaignsQuery(search, page ?? 1, pageSize ?? 50), ct)))).WithName("Promotions.ListCampaigns");
        g.MapGet("/campaigns/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetCampaignQuery(id), ct)))).WithName("Promotions.GetCampaign");
        g.MapPost("/campaigns", async (CampaignRules body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new SaveCampaignCommand(null, body, Actor(h)), ct)))).WithName("Promotions.CreateCampaign");
        g.MapPut("/campaigns/{id:guid}", async (Guid id, CampaignRules body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new SaveCampaignCommand(id, body, Actor(h)), ct)))).WithName("Promotions.UpdateCampaign");
        g.MapPut("/campaigns/{id:guid}/active", async (Guid id, ActiveCampaignBody body, HttpContext h, ISender sender, CancellationToken ct) => { await sender.Send(new SetCampaignActiveCommand(id, body.Active, Actor(h)), ct); return Results.Ok(ApiResponseHelper.Success(true)); }).WithName("Promotions.SetCampaignActive");
        g.MapGet("/campaigns/{id:guid}/codes", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ListCodesQuery(id, page ?? 1, pageSize ?? 100), ct)))).WithName("Promotions.ListCodes");
        g.MapPost("/campaigns/{id:guid}/codes", async (Guid id, GenerateCodesBody body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GenerateCodesCommand(id, body.PublicCode, body.UserId, body.Count, body.MaximumUses, body.IdempotencyKey, Actor(h)), ct)))).WithName("Promotions.GenerateCodes");
        g.MapPost("/campaigns/{id:guid}/codes/export", async (Guid id, ExportCodesBody body, HttpContext h, ISender sender, CancellationToken ct) => { h.Response.Headers.CacheControl = "no-store"; return Results.Ok(ApiResponseHelper.Success(await sender.Send(new ExportCodesCommand(id, body.BatchKey, Actor(h)), ct))); }).WithName("Promotions.ExportCodes");
        g.MapDelete("/campaigns/{id:guid}/codes/{codeId:guid}", async (Guid id, Guid codeId, HttpContext h, ISender sender, CancellationToken ct) => { await sender.Send(new DisableCodeCommand(id, codeId, Actor(h)), ct); return Results.Ok(ApiResponseHelper.Success(true)); }).WithName("Promotions.DisableCode");
        g.MapGet("/redemptions", async (Guid? orderId, Guid? userId, string? status, int? page, int? pageSize, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ListRedemptionsQuery(orderId, userId, status, page ?? 1, pageSize ?? 50), ct)))).WithName("Promotions.ListRedemptions");
    }
    private static Guid Actor(HttpContext h) => Guid.Parse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub")!);
}
