using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Roles.AssignRole;
using Refahi.Modules.Identity.Application.Features.Roles.RemoveRole;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Roles;

public class AssignRoleEndpoint : IEndpoint
{
    // POST /users/{userId}/roles - Assign role (Admin only)
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/users/{userId}/roles", async (
                Guid userId,
                [FromBody] AssignRoleRequest request,
                HttpContext httpContext,
                IMediator mediator) =>
        {
            var currentUserIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(currentUserIdClaim) || !Guid.TryParse(currentUserIdClaim, out var currentUserId))
                return Results.Unauthorized();

            var command = new AssignRoleCommand(userId, request.Role, currentUserId);
            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "Role assigned successfully"
            });
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.AssignRole")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public record AssignRoleRequest(string Role);
