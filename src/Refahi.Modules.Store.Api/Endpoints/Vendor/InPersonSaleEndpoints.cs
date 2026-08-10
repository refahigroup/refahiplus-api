using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;
using Refahi.Modules.Store.Api.Security;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Vendor;

public sealed record StartInPersonOrderRequest(Guid ShopId, Guid ProductId, string MobileNumber, long AmountMinor, string IdempotencyKey);
public sealed record VerifyInPersonOrderRequest(string OtpReferenceCode, string OtpCode, string IdempotencyKey);
public sealed record CancelInPersonOrderRequest(string IdempotencyKey);
public sealed record StartUserInPersonOrderRequest(Guid ShopId, Guid ProductId, long AmountMinor, string IdempotencyKey);

public sealed class InPersonSaleEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/vendor/shops/{shopId:guid}/in-person-products", async (Guid shopId,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(
                new GetInPersonProductsQuery(userId, shopId), ct)));
        }).WithName("Store.Vendor.InPersonProducts.List").WithTags("Store.Vendor.POS")
          .RequireAuthorization("VendorOnly").AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapGet("/v3/in-person/shops", async (ClaimsPrincipal principal,
            IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(
                new GetUserInPersonShopsQuery(userId), ct)));
        }).WithName("Store.V3.InPerson.Shops.List").WithTags("Store.V3.InPerson")
          .RequireAuthorization("UserOrAdmin").AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapGet("/v3/in-person/shops/{shopId:guid}/products", async (Guid shopId,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(
                new GetUserInPersonProductsQuery(userId, shopId), ct)));
        }).WithName("Store.V3.InPerson.Products.List").WithTags("Store.V3.InPerson")
          .RequireAuthorization("UserOrAdmin").AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapPost("/v3/in-person/orders", async (StartUserInPersonOrderRequest body,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var result = await mediator.Send(new StartUserInPersonOrderCommand(userId, body.ShopId,
                body.ProductId, body.AmountMinor, body.IdempotencyKey), ct);
            return Results.Created($"/api/store/v3/in-person/orders/{result.StoreOrderId}",
                ApiResponseHelper.Success(result, "سفارش حضوری ایجاد شد", 201));
        }).WithName("Store.V3.InPerson.Order.Start").WithTags("Store.V3.InPerson")
          .RequireAuthorization("UserOrAdmin").RequireRateLimiting("StoreCart")
          .AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapPost("/vendor/in-person-orders", async (StartInPersonOrderRequest body,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var result = await mediator.Send(new StartInPersonOrderCommand(userId, body.ShopId, body.ProductId,
                body.MobileNumber, body.AmountMinor, body.IdempotencyKey), ct);
            return Results.Created($"/api/store/vendor/in-person-orders/{result.OrderId}",
                ApiResponseHelper.Success(result, "سفارش فروش حضوری ایجاد شد", 201));
        }).WithName("Store.Vendor.InPersonOrder.Start").WithTags("Store.Vendor.POS")
          .RequireAuthorization("VendorOnly").RequireRateLimiting("VendorPos")
          .AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapPost("/vendor/in-person-orders/{orderId:guid}/verify", async (Guid orderId,
            VerifyInPersonOrderRequest body, ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(new VerifyInPersonOrderCommand(
                userId, orderId, body.OtpReferenceCode, body.OtpCode, body.IdempotencyKey), ct)));
        }).WithName("Store.Vendor.InPersonOrder.Verify").WithTags("Store.Vendor.POS")
          .RequireAuthorization("VendorOnly").RequireRateLimiting("VendorPos")
          .AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapPost("/vendor/in-person-orders/{orderId:guid}/resend-otp", async (Guid orderId,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(
                new ResendInPersonOrderOtpCommand(userId, orderId), ct)));
        }).WithName("Store.Vendor.InPersonOrder.ResendOtp").WithTags("Store.Vendor.POS")
          .RequireAuthorization("VendorOnly").RequireRateLimiting("VendorPos")
          .AddEndpointFilter<InPersonTypedErrorFilter>();

        routes.MapPost("/vendor/in-person-orders/{orderId:guid}/cancel", async (Guid orderId,
            CancelInPersonOrderRequest body, ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(
                new CancelInPersonOrderCommand(userId, orderId, body.IdempotencyKey), ct)));
        }).WithName("Store.Vendor.InPersonOrder.Cancel").WithTags("Store.Vendor.POS")
          .RequireAuthorization("VendorOnly").AddEndpointFilter<InPersonTypedErrorFilter>();
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out id);
}
