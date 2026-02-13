using Identity.Api.Services.Auth;
using Identity.Application.Features.Auth.Login;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Endpoints;

public class LoginEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        if (app == null)
            return;

        app.MapPost("/login", async (
                [FromBody] LoginCommand request,
                IMediator mediator,
                ITokenService tokenService) =>
        {

            if (request is null) 
                return Results.BadRequest();

            var user = await mediator.Send(request);

            if (user is null) 
                return Results.Unauthorized();

            var identity = new UserIdentity(user.Id, user.Username, user.Role);

            var tokens = tokenService.CreateTokens(identity);

            return Results.Ok(new
            {
                access_token = tokens.AccessToken,
                token_type = tokens.TokenType,
                expires_in = (int)(tokens.AccessTokenExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds,
                expires_at_utc = tokens.AccessTokenExpiresAtUtc,

                refresh_token = tokens.RefreshToken,
                refresh_expires_at_utc = tokens.RefreshTokenExpiresAtUtc
            });
        })
        .WithName("Identity.Login")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
