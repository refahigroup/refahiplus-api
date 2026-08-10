using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public sealed record UpdateVendorOrderStatusRequest(string Status);

public sealed class VendorOrdersEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/vendor",
                async (
                    int? page,
                    int? pageSize,
                    string? status,
                    string? paymentState,
                    string? orderNumber,
                    string? mobileNumber,
                    Guid? shopId,
                    DateTimeOffset? from,
                    DateTimeOffset? to,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetVendorOrdersQuery(
                            userId,
                            page ?? 1,
                            pageSize ?? 20,
                            status,
                            paymentState,
                            orderNumber,
                            mobileNumber,
                            shopId,
                            from,
                            to
                        ),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Orders.Vendor.List")
            .WithTags("Orders.Vendor")
            .RequireAuthorization("VendorOnly")
            .Produces<ApiResponse<VendorOrderPagedResult<VendorOrderSummaryDto>>>();

        routes
            .MapGet(
                "/vendor/summary",
                async (ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    return Results.Ok(
                        ApiResponseHelper.Success(
                            await mediator.Send(new GetVendorOrderDashboardQuery(userId), ct)
                        )
                    );
                }
            )
            .WithName("Orders.Vendor.Summary")
            .WithTags("Orders.Vendor")
            .RequireAuthorization("VendorOnly");

        routes
            .MapGet(
                "/vendor/{orderId:guid}",
                async (
                    Guid orderId,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetVendorOrderByIdQuery(userId, orderId),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("سفارش یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Orders.Vendor.Detail")
            .WithTags("Orders.Vendor")
            .RequireAuthorization("VendorOnly");

        routes
            .MapPut(
                "/vendor/{orderId:guid}/status",
                async (
                    Guid orderId,
                    UpdateVendorOrderStatusRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new UpdateVendorOrderStatusCommand(userId, orderId, body.Status),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("سفارش یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(result, "وضعیت سفارش بروزرسانی شد"));
                }
            )
            .WithName("Orders.Vendor.UpdateStatus")
            .WithTags("Orders.Vendor")
            .RequireAuthorization("VendorOnly");
    }

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"),
            out userId
        );
}
