using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Api.Services.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.Login;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Auth;

public class LoginEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/login", async (
                [FromBody] LoginCommand request,
                IMediator mediator,
                ITokenService tokenService,
                IRefreshTokenRepository refreshTokenRepository) =>
        {

            if (request is null)
                return Results.BadRequest();

            var user = await mediator.Send(request);

            if (user is null)
                return Results.Unauthorized();

            var identity = new UserIdentity(
                user.Id.ToString(),
                user.MobileNumber ?? user.Email ?? "Unknown",
                string.Join(",", user.Roles));

            var tokens = tokenService.CreateTokens(identity);

            // Store refresh token in database
            var refreshToken = Domain.Entities.RefreshToken.Create(
                userId: user.Id,
                token: tokens.RefreshToken,
                expiresAt: tokens.RefreshTokenExpiresAtUtc.HasValue ? tokens.RefreshTokenExpiresAtUtc.Value.DateTime : DateTime.UtcNow.AddDays(7)
            );

            await refreshTokenRepository.AddAsync(refreshToken);

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
