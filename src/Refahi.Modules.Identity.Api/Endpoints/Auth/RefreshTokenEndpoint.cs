using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Api.Services.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.RefreshToken;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Auth;

public class RefreshTokenEndpoint : IEndpoint
{
    // POST /refresh - Refresh access token using refresh token
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/refresh", async (
                [FromBody] RefreshTokenRequest request,
                IMediator mediator,
                ITokenService tokenService,
                IRefreshTokenRepository refreshTokenRepository) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.BadRequest(new { error = "Refresh token is required" });

            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await mediator.Send(command);

            if (!result.Success)
            {
                return Results.Json(new { error = result.ErrorMessage }, statusCode: StatusCodes.Status401Unauthorized);
            }

            // Generate new tokens using token service
            var userIdentity = new UserIdentity(
                Id: result.UserId.ToString()!,
                Username: result.Username!,
                Role: result.Roles?.Split(',').FirstOrDefault() ?? "User"
            );

            var tokenResult = tokenService.CreateTokens(userIdentity);

            // Store new refresh token in database
            var newRefreshToken = Domain.Entities.RefreshToken.Create(
                userId: result.UserId!.Value,
                token: tokenResult.RefreshToken,
                expiresAt: tokenResult.RefreshTokenExpiresAtUtc.HasValue ? tokenResult.RefreshTokenExpiresAtUtc.Value.DateTime : DateTime.UtcNow.AddDays(7)
            );

            await refreshTokenRepository.AddAsync(newRefreshToken, default);

            return Results.Ok(new
            {
                access_token = tokenResult.AccessToken,
                token_type = "Bearer",
                expires_in = (int)(tokenResult.AccessTokenExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds,
                expires_at_utc = tokenResult.AccessTokenExpiresAtUtc.DateTime,
                refresh_token = tokenResult.RefreshToken,
                refresh_expires_at_utc = tokenResult.RefreshTokenExpiresAtUtc.HasValue ? tokenResult.RefreshTokenExpiresAtUtc.Value.DateTime : (DateTime?)null
            });
        })
        .WithName("Identity.RefreshToken")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public record RefreshTokenRequest(string RefreshToken);
