using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Vendor;

public sealed record VendorStoreOrderLookupRequest(IReadOnlyList<Guid> OrderIds);

public sealed class VendorStoreOrderReadEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/vendor/store-orders/by-order/{orderId:guid}",
                async (
                    Guid orderId,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUser(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetVendorStoreOrderByOrderIdQuery(userId, orderId),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error(
                                "سفارش فروشگاه یافت نشد یا دسترسی به آن وجود ندارد",
                                statusCode: 404
                            )
                        )
                        : Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Store.Vendor.StoreOrders.ByOrderId")
            .WithTags("Store.Vendor.StoreOrders")
            .RequireAuthorization("VendorOnly")
            .AddEndpointFilter<InPersonTypedErrorFilter>()
            .Produces<ApiResponse<VendorStoreOrderSnapshotDto>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        routes
            .MapPost(
                "/vendor/store-orders/lookup",
                async (
                    VendorStoreOrderLookupRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUser(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetVendorStoreOrdersByOrderIdsQuery(userId, body.OrderIds),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Store.Vendor.StoreOrders.Lookup")
            .WithTags("Store.Vendor.StoreOrders")
            .RequireAuthorization("VendorOnly")
            .AddEndpointFilter<InPersonTypedErrorFilter>()
            .Produces<ApiResponse<IReadOnlyList<VendorStoreOrderSnapshotDto>>>(
                StatusCodes.Status200OK
            );
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid id) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"),
            out id
        );
}
