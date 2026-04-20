using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public class GetCartEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/cart", async (
            string moduleSlug,
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

            var result = await mediator.Send(new GetCartQuery(userId, moduleId.Value), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetCart")
        .WithTags("Store.Cart")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<CartDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
