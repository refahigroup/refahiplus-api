using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Domain.Entities;

/// <summary>
/// UserRole entity - represents a role assigned to a user
/// </summary>
public class UserRole
{
    // Private constructor for EF
    private UserRole() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public DateTime AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    /// <summary>
    /// Factory method to create a new user role
    /// </summary>
    public static UserRole Create(Guid userId, string role, Guid? assignedBy = null)
    {
        if (!Roles.IsValid(role))
            throw new DomainException($"Invalid role: {role}", "INVALID_ROLE");

        return new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = role,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };
    }
}
