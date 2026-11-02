using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Order.GetOrder;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Checkout;

public sealed class GetOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/purchases/{entertainmentsOrderId:guid}",
                async (
                    Guid entertainmentsOrderId,
                    bool revealTickets,
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    c.Response.Headers.CacheControl = "private, no-store";

                    var value = await m.Send(new GetEntertainmentPurchaseQuery(userId, _Helpers.Role(c), entertainmentsOrderId, revealTickets), ct);

                    return value is null
                        ? Results.NotFound(ApiResponseHelper.Error("سفارش یافت نشد", statusCode: 404))
                        : Results.Ok(ApiResponseHelper.Success(value));
                }

            )
            .WithName("Entertainment.GetOrder")
            .WithTags("Entertainment.Orders")
            .RequireAuthorization("UserOrAdmin");
    }
}

