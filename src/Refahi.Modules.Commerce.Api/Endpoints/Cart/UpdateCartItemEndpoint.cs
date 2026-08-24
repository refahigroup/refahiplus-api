using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Catalog;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Cart.UpdateItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

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

                    var result = await m.Send(new UpdateCommerceCartItemCommand(userId, itemId, body.Quantity), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));

                }
            )
            .WithName("Commerce.UpdateCartItem")
            .WithTags("Commerce.Cart")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record UpdateCartItemBody(int Quantity);
}

