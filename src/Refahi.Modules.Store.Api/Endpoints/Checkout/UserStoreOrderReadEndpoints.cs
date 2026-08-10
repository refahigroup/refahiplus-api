using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Checkout;

public sealed record UserStoreOrderLookupRequest(IReadOnlyList<Guid> OrderIds);

public sealed class UserStoreOrderReadEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/v3/store-orders/by-order/{orderId:guid}",
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
                        new GetUserStoreOrderByOrderIdQuery(
                            userId,
                            orderId,
                            principal.IsInRole("Admin")
                        ),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error(
                                "سفارش فروشگاه یافت نشد یا متعلق به شما نیست",
                                statusCode: 404
                            )
                        )
                        : Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Store.V3.StoreOrders.ByOrderId")
            .WithTags("Store.V3.StoreOrders")
            .RequireAuthorization("UserOrAdmin")
            .AddEndpointFilter<InPersonTypedErrorFilter>()
            .Produces<ApiResponse<VendorStoreOrderSnapshotDto>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        routes
            .MapPost(
                "/v3/store-orders/lookup",
                async (
                    UserStoreOrderLookupRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUser(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetUserStoreOrdersByOrderIdsQuery(
                            userId,
                            body.OrderIds,
                            principal.IsInRole("Admin")
                        ),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Store.V3.StoreOrders.Lookup")
            .WithTags("Store.V3.StoreOrders")
            .RequireAuthorization("UserOrAdmin")
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
