using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Contracts.Features.Profile.UpdateMe;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Profile;

public class UpdateMeEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPut("/me", async (
            HttpContext httpContext,
            [FromBody] UpdateMeRequest request,
            IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var command = new UpdateMeCommand(userId, request.FirstName, request.LastName, request.Email);
            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(ApiResponseHelper.Error(result.ErrorMessage ?? "خطا در بروزرسانی اطلاعات"));

            return Results.Ok(ApiResponseHelper.Success(result.Me, "اطلاعات حساب با موفقیت ذخیره شد"));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Identity.UpdateMe")
        .WithTags("Identity.Profile")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public record UpdateMeRequest(string FirstName, string LastName, string? Email);
