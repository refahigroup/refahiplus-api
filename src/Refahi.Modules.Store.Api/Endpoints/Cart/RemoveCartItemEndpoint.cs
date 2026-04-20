using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public class RemoveCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/{moduleSlug}/cart/items/{id:guid}", async (
            string moduleSlug,
            Guid id,
            HttpContext httpContext,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var command = new RemoveCartItemCommand(userId, moduleId.Value, id);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "آیتم از سبد خرید حذف شد"));
        })
        .WithName("Store.RemoveCartItem")
        .WithTags("Store.Cart")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<RemoveCartItemResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
