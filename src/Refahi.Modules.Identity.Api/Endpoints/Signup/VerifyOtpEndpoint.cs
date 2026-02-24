using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Auth.SignUp;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Signup;

public class VerifyOtpEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/signup/verify-otp", async (
                [FromBody] VerifyOtpRequest request,
                IMediator mediator) =>
        {
            if (request == null)
                return Results.BadRequest("Request body is required");

            var command = new ValidateOtpAndCreateUserCommand(
                request.Token,
                request.OtpCode);

            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "User created successfully",
                user = result.User
            });
        })
        .WithName("Identity.SignUp.VerifyOtp")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public record VerifyOtpRequest(
    string Token,
    string OtpCode);
