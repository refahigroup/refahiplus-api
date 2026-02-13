
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Services.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        // Fail fast: never allow missing/weak secret.
        if (string.IsNullOrWhiteSpace(_options.Key))
            throw new InvalidOperationException("Jwt:Key is missing.");
        if (_options.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

        var keyBytes = Encoding.UTF8.GetBytes(_options.Key);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    }

    public TokenResult CreateTokens(UserIdentity user)
    {
        var now = DateTimeOffset.UtcNow;

        var accessExp = now.AddMinutes(_options.AccessTokenExpiryMinutes);
        var jti = Guid.NewGuid().ToString("N");

        // Standard-ish claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),

            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExp.UtcDateTime,
            signingCredentials: _signingCredentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token (optional but recommended)
        var refreshToken = GenerateSecureRefreshToken();
        var refreshExp = now.AddMinutes(_options.RefreshTokenExpiryMinutes);

        return new TokenResult(
            AccessToken: accessToken,
            AccessTokenExpiresAtUtc: accessExp,
            TokenType: "Bearer",
            RefreshToken: refreshToken,
            RefreshTokenExpiresAtUtc: refreshExp
        );
    }

    private static string GenerateSecureRefreshToken()
    {
        // 32 bytes => 256-bit
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes.ToArray()); // URL-safe
    }
}

