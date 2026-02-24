using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Auth.SignUp;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Signup;

public class SendOtpEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/signup/send-otp", async (
                [FromBody] SendOtpRequest request,
                IMediator mediator) =>
        {
            if (request == null)
                return Results.BadRequest("Request body is required");

            var command = new SendOtpCommand(
                request.MobileNumber,
                request.Email);

            var result = await mediator.Send(command);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                success = true,
                message = "OTP sent successfully",
                token = result.Token,
                expiresAt = result.ExpiresAt
            });
        })
        .WithName("Identity.SignUp.SendOtp")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public record SendOtpRequest(
    string? MobileNumber,
    string? Email);
