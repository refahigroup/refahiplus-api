using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public class UpdateCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/{moduleSlug}/cart/items/{id:guid}", async (
            string moduleSlug,
            Guid id,
            [FromBody] UpdateCartItemCommand command,
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

            var adjustedCommand = command with { UserId = userId, ModuleId = moduleId.Value, CartItemId = id };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سبد خرید بروزرسانی شد"));
        })
        .WithName("Store.UpdateCartItem")
        .WithTags("Store.Cart")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<UpdateCartItemResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
