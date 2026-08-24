using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Cart;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Order.GetOrder;
using Refahi.Modules.Commerce.Domain;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Checkout;

public sealed class GetOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/orders/{commerceOrderId:guid}",
                async (
                    Guid commerceOrderId,
                    bool revealTickets,
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    var value = await m.Send(new GetCommerceOrderQuery(userId, _Helpers.Role(c), commerceOrderId, revealTickets), ct);

                    return value is null
                        ? Results.NotFound(ApiResponseHelper.Error("سفارش یافت نشد", statusCode: 404))
                        : Results.Ok(ApiResponseHelper.Success(value));
                }

            )
            .WithName("Commerce.GetOrder")
            .WithTags("Commerce.Orders")
            .RequireAuthorization("UserOrAdmin");
    }
}

