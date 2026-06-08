using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Auth;

public class SendLoginOtpEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/login/send-otp", async (
                [FromBody] SendLoginOtpCommand request,
                IMediator mediator) =>
        {
            if (request is null)
                return Results.BadRequest();

            var result = await mediator.Send(request);

            if (!result.Success)
                return Results.BadRequest(ApiResponseHelper.Error(result.ErrorMessage ?? "ارسال کد ورود ناموفق بود"));

            return Results.Ok(new
            {
                success = true,
                token = result.Token,
                expires_at = result.ExpiresAt,
                flow = result.Flow
            });
        })
        .WithName("Identity.Auth.SendLoginOtp")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
