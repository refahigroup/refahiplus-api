using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Profile.CreateOrUpdate;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Profile;

public class CreateOrUpdateProfileEndpoint : IEndpoint
{
    // POST /profile - Create or update profile
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/profile", async (
                HttpContext httpContext,
                [FromBody] CreateOrUpdateProfileRequest request,
                IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var command = new CreateOrUpdateProfileCommand(
                userId,
                request.FirstName,
                request.LastName,
                request.NationalCode,
                request.Gender);

            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "Profile saved successfully",
                profile = result.Profile
            });
        })
        .RequireAuthorization()
        .WithName("Identity.CreateOrUpdateProfile")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
