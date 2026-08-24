using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Catalog;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Cart.Clear;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

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

                    await m.Send(new ClearCommerceCartCommand(userId), ct);

                    return Results.Ok(ApiResponseHelper.Success(true));
                }
            )
            .WithName("Commerce.ClearCart")
            .WithTags("Commerce.Cart")
            .RequireAuthorization("UserOrAdmin");
    }
}

