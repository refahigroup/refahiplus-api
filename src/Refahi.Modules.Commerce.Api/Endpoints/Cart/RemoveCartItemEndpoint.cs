using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Cart.RemoveItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

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

                var result = await m.Send(new RemoveCommerceCartItemCommand(userId, itemId), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Commerce.RemoveCartItem")
            .WithTags("Commerce.Cart")
            .RequireAuthorization("UserOrAdmin");
    }
}

