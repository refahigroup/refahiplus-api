using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Auth.SetPassword;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Auth;

public class SetPasswordEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/setpassword", async (
                [FromBody] SetPasswordRequest request,
                IMediator mediator) =>
        {
            if (request == null)
                return Results.BadRequest("Request body is required");

            var command = new SetPasswordCommand(
                request.MobileOrEmail,
                request.Password);

            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "Password set successfully"
            });
        })
        .WithName("Identity.SetPassword")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public record SetPasswordRequest(
    string MobileOrEmail,
    string Password);
