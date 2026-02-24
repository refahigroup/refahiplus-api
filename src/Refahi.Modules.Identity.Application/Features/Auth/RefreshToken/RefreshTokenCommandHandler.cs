using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Note: Basic validation handled by FluentValidation

        // Find refresh token in database
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken == null)
        {
            return new RefreshTokenResult(false, "Invalid refresh token");
        }

        // Validate refresh token
        if (!refreshToken.IsValid)
        {
            if (refreshToken.IsExpired)
            {
                return new RefreshTokenResult(false, "Refresh token has expired");
            }

            if (refreshToken.IsRevoked)
            {
                return new RefreshTokenResult(false, "Refresh token has been revoked");
            }

            if (refreshToken.IsUsed)
            {
                return new RefreshTokenResult(false, "Refresh token has already been used");
            }

            return new RefreshTokenResult(false, "Invalid refresh token");
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);

        if (user == null)
        {
            return new RefreshTokenResult(false, "User not found");
        }

        if (!user.IsActive)
        {
            return new RefreshTokenResult(false, "User account is inactive");
        }

        // Mark old refresh token as used
        refreshToken.MarkAsUsed();
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);

        // Return user data - token generation will be handled in the endpoint layer
        return new RefreshTokenResult(
            Success: true,
            AccessToken: null,  // Will be set by endpoint
            AccessTokenExpiresAt: null,  // Will be set by endpoint
            RefreshToken: null,  // Will be set by endpoint
            RefreshTokenExpiresAt: null,  // Will be set by endpoint
            UserId: user.Id,
            Username: user.MobileNumber ?? user.Email ?? "",
            Roles: string.Join(",", user.Roles?.Select(r => r.Role) ?? new[] { "User" })
        );
    }
}
