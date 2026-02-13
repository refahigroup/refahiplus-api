namespace Identity.Api.Services.Auth;

public sealed record TokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string TokenType = "Bearer",
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAtUtc = null
);

