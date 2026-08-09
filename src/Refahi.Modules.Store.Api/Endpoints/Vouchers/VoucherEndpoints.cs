using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Vouchers;

public sealed record RedeemVoucherRequest(Guid ShopId, string Code, string IdempotencyKey);
public sealed record VendorVoucherHistoryRequest(
    Guid SupplierId, Guid? ShopId = null, int Page = 1, int PageSize = 20);
public sealed record OverrideRedeemedVoucherRefundRequest(string Reason, string IdempotencyKey);

public sealed class VoucherEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/v3/vouchers", async (ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponseHelper.Success(await mediator.Send(new GetMyVouchersQuery(userId), ct)));
        }).WithName("Store.V3.Vouchers.List").WithTags("Store.V3.Vouchers")
          .RequireAuthorization("UserOrAdmin").AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<IReadOnlyList<VoucherDto>>>(StatusCodes.Status200OK);

        routes.MapGet("/v3/vouchers/{voucherId:guid}", async (Guid voucherId, ClaimsPrincipal principal,
            IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var result = await mediator.Send(new GetMyVoucherQuery(userId, voucherId), ct);
            return result is null
                ? Results.NotFound(new VoucherErrorResponse(false, "VOUCHER_NOT_FOUND",
                    "ووچر یافت نشد یا متعلق به شما نیست", 404))
                : Results.Ok(ApiResponseHelper.Success(result));
        }).WithName("Store.V3.Vouchers.Detail").WithTags("Store.V3.Vouchers")
          .RequireAuthorization("UserOrAdmin").AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<VoucherDto>>(StatusCodes.Status200OK)
          .Produces<VoucherErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapPost("/vendor/vouchers/redeem", async (RedeemVoucherRequest body,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var result = await mediator.Send(new RedeemVoucherCommand(
                userId, body.ShopId, body.Code, body.IdempotencyKey), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "ووچر با موفقیت استفاده شد"));
        }).WithName("Store.Vendor.Vouchers.Redeem").WithTags("Store.Vendor.Vouchers")
          .RequireAuthorization("VendorOnly").RequireRateLimiting("VoucherRedeem")
          .AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<VoucherRedemptionDto>>(StatusCodes.Status200OK)
          .Produces<VoucherErrorResponse>(StatusCodes.Status409Conflict);

        routes.MapGet("/vendor/vouchers/history", async ([AsParameters] VendorVoucherHistoryRequest query,
            ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            var result = await mediator.Send(
                new GetVendorVoucherRedemptionHistoryQuery(
                    userId, query.SupplierId, query.ShopId, query.Page, query.PageSize), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        }).WithName("Store.Vendor.Vouchers.History").WithTags("Store.Vendor.Vouchers")
          .RequireAuthorization("VendorOnly").RequireRateLimiting("VoucherRedeem")
          .AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<VoucherRedemptionHistoryPageDto>>(StatusCodes.Status200OK);

        routes.MapGet("/admin/vouchers", async (Guid? storeOrderId, Guid? voucherId,
            IMediator mediator, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(
                await mediator.Send(new GetAdminVoucherAuditQuery(storeOrderId, voucherId), ct))))
          .WithName("Store.Admin.Vouchers.Audit").WithTags("Store.Admin.Vouchers")
          .RequireAuthorization("AdminOnly").AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<IReadOnlyList<VoucherAuditDto>>>(StatusCodes.Status200OK);

        routes.MapGet("/admin/store-orders/by-order/{orderId:guid}/refund", async (
            Guid orderId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminStoreOrderRefundQuery(orderId), ct);
            return result is null
                ? Results.NotFound(new VoucherErrorResponse(false, "STORE_ORDER_NOT_FOUND",
                    "سفارش فروشگاه یافت نشد", StatusCodes.Status404NotFound))
                : Results.Ok(ApiResponseHelper.Success(result));
        }).WithName("Store.Admin.StoreOrders.RefundDetail").WithTags("Store.Admin.VoucherRefunds")
          .RequireAuthorization("AdminOnly").AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<AdminStoreOrderRefundDto>>(StatusCodes.Status200OK)
          .Produces<VoucherErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapPost("/admin/store-orders/by-order/{orderId:guid}/voucher-refund-override", async (
            Guid orderId, OverrideRedeemedVoucherRefundRequest body, ClaimsPrincipal principal,
            IMediator mediator, CancellationToken ct) =>
        {
            if (!TryUser(principal, out var adminUserId)) return Results.Unauthorized();
            var result = await mediator.Send(new OverrideRedeemedVoucherRefundCommand(
                orderId, adminUserId, body.Reason, body.IdempotencyKey), ct);
            var response = ApiResponseHelper.Success(result,
                result.Outcome == "RefundCompleted"
                    ? "بازگشت وجه استثنایی با موفقیت تکمیل شد"
                    : "مجوز ثبت شد؛ بازگشت وجه نیازمند تلاش مجدد یا بررسی عملیاتی است");
            return result.Outcome == "RefundCompleted"
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status202Accepted);
        }).WithName("Store.Admin.StoreOrders.OverrideRedeemedVoucherRefund")
          .WithTags("Store.Admin.VoucherRefunds")
          .RequireAuthorization("AdminOnly").AddEndpointFilter<VoucherTypedErrorFilter>()
          .Produces<ApiResponse<VoucherRefundOverrideDto>>(StatusCodes.Status200OK)
          .Produces<ApiResponse<VoucherRefundOverrideDto>>(StatusCodes.Status202Accepted)
          .Produces<VoucherErrorResponse>(StatusCodes.Status409Conflict)
          .Produces(StatusCodes.Status403Forbidden);
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out id);
}
