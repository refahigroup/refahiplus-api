using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Media.Api.Endpoints;

public class DeleteMediaEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext http,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue("sub");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var isAdmin = http.User.IsInRole("Admin");
            await mediator.Send(new DeleteMediaCommand(id, userId, isAdmin), ct);
            return Results.NoContent();
        })
        .WithName("Media.Delete")
        .WithTags("Media")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
