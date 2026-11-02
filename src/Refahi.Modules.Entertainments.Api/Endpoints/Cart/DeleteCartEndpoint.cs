using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Catalog;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Cart.Clear;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Cart;

public sealed class DeleteCartEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapDelete(
                "/cart",
                async (
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    await m.Send(new ClearEntertainmentCartCommand(userId), ct);

                    return Results.Ok(ApiResponseHelper.Success(true));
                }
            )
            .WithName("Entertainment.ClearCart")
            .WithTags("Entertainment.Cart")
            .RequireAuthorization("UserOrAdmin");
    }
}

