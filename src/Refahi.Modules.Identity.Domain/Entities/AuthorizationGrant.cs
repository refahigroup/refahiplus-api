using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Domain.Entities;

public sealed class AuthorizationGrant
{
    private AuthorizationGrant() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? EmittedRole { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }

    public static AuthorizationGrant Create(
        Guid userId,
        string issuer,
        string value,
        string? emittedRole,
        Guid? createdBy
    )
    {
        if (userId == Guid.Empty)
            throw new DomainException("شناسه کاربر الزامی است", "INVALID_GRANT_USER");
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Trim().Length > 64)
            throw new DomainException("صادرکننده Grant نامعتبر است", "INVALID_GRANT_ISSUER");
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 256)
            throw new DomainException("مقدار Grant نامعتبر است", "INVALID_GRANT_VALUE");
        if (!string.IsNullOrWhiteSpace(emittedRole) && !Roles.IsValid(emittedRole))
            throw new DomainException("Role عمومی Grant نامعتبر است", "INVALID_GRANT_ROLE");

        return new AuthorizationGrant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Issuer = issuer.Trim(),
            Value = value.Trim(),
            EmittedRole = string.IsNullOrWhiteSpace(emittedRole) ? null : emittedRole.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public void Reactivate(string? emittedRole, Guid? actorId)
    {
        if (!string.IsNullOrWhiteSpace(emittedRole) && !Roles.IsValid(emittedRole))
            throw new DomainException("Role عمومی Grant نامعتبر است", "INVALID_GRANT_ROLE");

        EmittedRole = string.IsNullOrWhiteSpace(emittedRole) ? null : emittedRole.Trim();
        IsActive = true;
        RevokedAt = null;
        RevokedBy = null;
        CreatedBy ??= actorId;
    }

    public void Revoke(Guid? actorId)
    {
        if (!IsActive)
            return;
        IsActive = false;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedBy = actorId;
    }
}
