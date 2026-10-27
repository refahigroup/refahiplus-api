using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Cart.RemoveItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Cart;

public sealed class RemoveCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapDelete(
            "/cart/items/{itemId:guid}",
            async (
                Guid itemId,
                HttpContext c,
                IMediator m,
                CancellationToken ct
            ) =>
            {
                if (!_Helpers.TryUser(c, out var userId))
                    return _Helpers.Unauthorized();

                var result = await m.Send(new RemoveEntertainmentCartItemCommand(userId, itemId), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Entertainment.RemoveCartItem")
            .WithTags("Entertainment.Cart")
            .RequireAuthorization("UserOrAdmin");
    }
}

