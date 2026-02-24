using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Roles.RemoveRole;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints;

public class RemoveRoleEndpoint : IEndpoint
{
    // DELETE /users/{userId}/roles/{role} - Remove role (Admin only)
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapDelete("/users/{userId}/roles/{role}", async (
                Guid userId,
                string role,
                IMediator mediator) =>
        {
            var command = new RemoveRoleCommand(userId, role);
            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "Role removed successfully"
            });
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.RemoveRole")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}