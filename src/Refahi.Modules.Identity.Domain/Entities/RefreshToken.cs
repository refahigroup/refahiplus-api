
namespace Refahi.Modules.Identity.Domain.Entities;

/// <summary>
/// Represents a refresh token for JWT authentication
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }

    // EF Core
    private RefreshToken() { }

    private RefreshToken(
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        IsRevoked = false;
        IsUsed = false;
    }

    public static RefreshToken Create(
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiry date must be in the future", nameof(expiresAt));

        return new RefreshToken(userId, token, expiresAt);
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsValid => !IsExpired && !IsRevoked && !IsUsed;

    public void MarkAsUsed()
    {
        if (IsUsed)
            throw new InvalidOperationException("Refresh token has already been used");

        if (IsRevoked)
            throw new InvalidOperationException("Refresh token has been revoked");

        if (IsExpired)
            throw new InvalidOperationException("Refresh token has expired");

        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }

    public void Revoke(string reason)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Refresh token has already been revoked");

        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason;
    }
}
