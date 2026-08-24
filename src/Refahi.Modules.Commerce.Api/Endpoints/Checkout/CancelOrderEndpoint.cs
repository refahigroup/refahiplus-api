using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Cart;
using Refahi.Modules.Commerce.Api.Endpoints.Catalog;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Checkout;

public sealed class CancelOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.
            MapPost(
                "/orders/{commerceOrderId:guid}/cancel", 
                async (
                    Guid commerceOrderId, 
                    CancelCommerceOrderBody body, 
                    HttpContext c, 
                    IMediator m, 
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    var result = await m.Send(new CancelCommerceOrderCommand(userId, _Helpers.Role(c), commerceOrderId, body.Reason, body.IdempotencyKey), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));

                }
            )
            .WithName("Commerce.CancelOrder")
            .WithTags("Commerce.Orders")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record CancelCommerceOrderBody(string? Reason, string IdempotencyKey);
}

