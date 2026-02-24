using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Auth.RefreshToken;

/// <summary>
/// Command to refresh access token using a valid refresh token
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResult>;

public record RefreshTokenResult(
    bool Success,
    string? ErrorMessage = null,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null,
    Guid? UserId = null,
    string? Username = null,
    string? Roles = null);
