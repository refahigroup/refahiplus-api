using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Api.Services.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Auth;

public class VerifyLoginOtpEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/login/verify-otp", async (
                [FromBody] VerifyLoginOtpCommand request,
                IMediator mediator,
                ITokenService tokenService,
                IRefreshTokenRepository refreshTokenRepository) =>
        {
            if (request is null)
                return Results.BadRequest();

            var result = await mediator.Send(request);

            if (!result.Success || result.User is null)
                return Results.BadRequest(ApiResponseHelper.Error(result.ErrorMessage ?? "کد تأیید نامعتبر است"));

            var identity = new UserIdentity(
                result.User.Id.ToString(),
                result.User.MobileNumber ?? result.User.Email ?? "Unknown",
                string.Join(",", result.User.Roles));

            var tokens = tokenService.CreateTokens(identity);

            var refreshToken = Domain.Entities.RefreshToken.Create(
                userId: result.User.Id,
                token: tokens.RefreshToken,
                expiresAt: tokens.RefreshTokenExpiresAtUtc.HasValue
                    ? tokens.RefreshTokenExpiresAtUtc.Value.UtcDateTime
                    : DateTime.UtcNow.AddDays(7));

            await refreshTokenRepository.AddAsync(refreshToken);

            return Results.Ok(new
            {
                access_token = tokens.AccessToken,
                token_type = tokens.TokenType,
                expires_in = (int)(tokens.AccessTokenExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds,
                expires_at_utc = tokens.AccessTokenExpiresAtUtc,

                refresh_token = tokens.RefreshToken,
                refresh_expires_at_utc = tokens.RefreshTokenExpiresAtUtc,

                user = result.User,
                is_new_user = result.IsNewUser,
                registration_completed = result.RegistrationCompleted,
                profile_required = result.ProfileRequired
            });
        })
        .WithName("Identity.Auth.VerifyLoginOtp")
        .WithTags("Identity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
