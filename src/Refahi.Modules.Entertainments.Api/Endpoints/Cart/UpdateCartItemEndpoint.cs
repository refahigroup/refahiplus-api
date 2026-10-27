using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Catalog;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Cart.UpdateItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Cart;

public sealed class UpdateCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapPatch(
                "/cart/items/{itemId:guid}",
                async (
                    Guid itemId,
                    UpdateCartItemBody body,
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    var result = await m.Send(new UpdateEntertainmentCartItemCommand(userId, itemId, body.Quantity), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));

                }
            )
            .WithName("Entertainment.UpdateCartItem")
            .WithTags("Entertainment.Cart")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record UpdateCartItemBody(int Quantity);
}

