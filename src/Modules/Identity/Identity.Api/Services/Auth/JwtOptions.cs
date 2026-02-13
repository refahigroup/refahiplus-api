using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Services.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; init; } = default!; // Symmetric secret (>= 32 chars)

    [Required]
    public string Issuer { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    [Range(1, 24 * 60)]
    public int AccessTokenExpiryMinutes { get; init; } = 60;

    [Range(1, 30 * 24 * 60)]
    public int RefreshTokenExpiryMinutes { get; init; } = 7 * 24 * 60;

    // Optional hardening
    public int ClockSkewSeconds { get; init; } = 30;
}
