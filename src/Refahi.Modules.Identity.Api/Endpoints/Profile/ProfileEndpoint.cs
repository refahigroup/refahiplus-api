using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Profile.CreateOrUpdate;
using Refahi.Modules.Identity.Application.Features.Profile.GetProfile;
using Refahi.Modules.Identity.Domain.ValueObjects;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Profile;

public class ProfileEndpoint : IEndpoint
{
    // GET /profile - Get current user's profile
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/profile", async (
                HttpContext httpContext,
                IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var query = new GetProfileQuery(userId);
            var result = await mediator.Send(query);

            if (!result.Success)
                return Results.NotFound(new { error = result.ErrorMessage });

            return Results.Ok(result.Profile);
        })
        .RequireAuthorization()
        .WithName("Identity.GetProfile")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}