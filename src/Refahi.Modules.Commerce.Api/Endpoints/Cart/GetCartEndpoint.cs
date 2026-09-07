using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Cart.GetCart;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

public sealed class GetCartEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/cart",
            async (
                HttpContext c,
                IMediator m,
                CancellationToken ct
            ) =>
            {
                return _Helpers.TryUser(c, out var userId)
                    ? Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCommerceCartQuery(userId), ct)))
                    : _Helpers.Unauthorized();
            })
            .WithName("Commerce.GetCart")
            .WithTags("Commerce.Cart")
            .RequireAuthorization("UserOrAdmin");
    }
}

