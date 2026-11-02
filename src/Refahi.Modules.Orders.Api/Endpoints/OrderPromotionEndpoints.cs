using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Orders.Api.Endpoints;

public sealed record PromotionRequest(string Code, long ExpectedPricingVersion, string? IdempotencyKey = null);
public sealed record RemovePromotionRequest(long ExpectedPricingVersion, string IdempotencyKey);
public sealed class OrderPromotionEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        var group = routes.MapGroup("/{orderId:guid}/promotion").RequireAuthorization("UserOrAdmin").RequireRateLimiting("OrderPromotions").AddEndpointFilter<PromotionIpRateLimitFilter>().WithTags("Orders.Promotions");
        group.MapPost("/preview", async (Guid orderId, PromotionRequest body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new PreviewOrderPromotionQuery(orderId, User(h), body.Code, body.ExpectedPricingVersion), ct)))).WithName("Orders.PreviewPromotion");
        group.MapPut("/", async (Guid orderId, PromotionRequest body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ApplyOrderPromotionCommand(orderId, User(h), body.Code, body.ExpectedPricingVersion, body.IdempotencyKey ?? ""), ct)))).WithName("Orders.ApplyPromotion");
        group.MapDelete("/", async (Guid orderId, [FromBody] RemovePromotionRequest body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new RemoveOrderPromotionCommand(orderId, User(h), body.ExpectedPricingVersion, body.IdempotencyKey), ct)))).WithName("Orders.RemovePromotion");
    }
    private static Guid User(HttpContext h) => Guid.Parse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub")!);
}
