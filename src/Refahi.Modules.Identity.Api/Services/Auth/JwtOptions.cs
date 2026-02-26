using Refahi.Shared.Extensions;
using System.ComponentModel.DataAnnotations;

namespace Refahi.Modules.Identity.Api.Services.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    private string _key = string.Empty; // Symmetric secret (>= 32 chars)

    [Required, MinLength(32)]
    public string Key 
    {
        get => _key;
        init
        {
            _key = value.ReplaceWithEnvironmentVariables();
        }
    } 

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

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Key) && Key.Length >= 32;
}
