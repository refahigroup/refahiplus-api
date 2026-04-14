using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public class RemoveCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/cart/items/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var command = new RemoveCartItemCommand(userId, id);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "آیتم از سبد خرید حذف شد"));
        })
        .WithName("Store.RemoveCartItem")
        .WithTags("Store.Cart")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<RemoveCartItemResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
