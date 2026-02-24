using Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Domain.Aggregates;

/// <summary>
/// User Aggregate Root - represents a user in the system
/// </summary>
public class User
{
    // Private constructor for EF
    private User() 
    {
        Roles = new List<UserRole>();
    }

    public Guid Id { get; private set; }
    public string? MobileNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Username { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public bool MobileApproved { get; private set; }
    public bool EmailApproved { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    
    // Navigation properties
    public UserProfile? Profile { get; private set; }
    public ICollection<UserRole> Roles { get; private set; }

    /// <summary>
    /// Factory method for creating a new user
    /// </summary>
    public static User Create(string? mobileNumber, string? email)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber) && string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Either mobile number or email must be provided", "MOBILE_OR_EMAIL_REQUIRED");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            MobileNumber = mobileNumber?.Trim(),
            Email = email?.Trim().ToLower(),
            IsActive = true,
            MobileApproved = false,
            EmailApproved = false,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Roles = new List<UserRole>()
        };
    }

    /// <summary>
    /// Set password for the user
    /// </summary>
    public void SetPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 8)
        {
            throw new DomainException("Password must be at least 8 characters", "WEAK_PASSWORD");
        }

        PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Verify password
    /// </summary>
    public bool VerifyPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(PasswordHash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(plainPassword, PasswordHash);
    }

    /// <summary>
    /// Activate the user account
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivate the user account
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Assign a role to the user (idempotent)
    /// </summary>
    public void AssignRole(string role, Guid? assignedBy = null)
    {
        // Check if role already exists
        if (Roles.Any(r => r.Role == role))
        {
            return; // Idempotent
        }

        var userRole = UserRole.Create(Id, role, assignedBy);
        Roles.Add(userRole);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove a role from the user (idempotent)
    /// </summary>
    public void RemoveRole(string role)
    {
        var userRole = Roles.FirstOrDefault(r => r.Role == role);
        if (userRole != null)
        {
            Roles.Remove(userRole);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Get all roles for the user
    /// </summary>
    public IReadOnlyList<string> GetRoles()
    {
        return Roles.Select(r => r.Role).ToList();
    }

    /// <summary>
    /// Set username for the user (must be unique, validated by repository)
    /// </summary>
    public void SetUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username cannot be empty", "USERNAME_REQUIRED");
        }

        if (username.Length < 3 || username.Length > 30)
        {
            throw new DomainException("Username must be between 3 and 30 characters", "INVALID_USERNAME_LENGTH");
        }

        // Username can only contain letters, numbers, underscore, and dash
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
        {
            throw new DomainException("Username can only contain letters, numbers, underscore, and dash", "INVALID_USERNAME_FORMAT");
        }

        Username = username.Trim().ToLower();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Change password (requires old password verification)
    /// </summary>
    public void ChangePassword(string oldPassword, string newPassword)
    {
        if (!VerifyPassword(oldPassword))
        {
            throw new DomainException("Current password is incorrect", "INVALID_OLD_PASSWORD");
        }

        SetPassword(newPassword);
    }

    /// <summary>
    /// Approve mobile number
    /// </summary>
    public void ApproveMobile()
    {
        if (string.IsNullOrWhiteSpace(MobileNumber))
        {
            throw new DomainException("Mobile number is not set", "MOBILE_NOT_SET");
        }

        MobileApproved = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Approve email
    /// </summary>
    public void ApproveEmail()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            throw new DomainException("Email is not set", "EMAIL_NOT_SET");
        }

        EmailApproved = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Lock user account for specified duration (e.g., after failed login attempts)
    /// </summary>
    public void Lock(TimeSpan duration)
    {
        LockedUntil = DateTime.UtcNow.Add(duration);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unlock user account
    /// </summary>
    public void Unlock()
    {
        LockedUntil = null;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if user is currently locked
    /// </summary>
    public bool IsLocked()
    {
        return LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Increment failed login attempts and lock if threshold exceeded
    /// </summary>
    public void RecordFailedLoginAttempt(int maxAttempts = 5, TimeSpan? lockDuration = null)
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTime.UtcNow;

        if (FailedLoginAttempts >= maxAttempts)
        {
            Lock(lockDuration ?? TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    /// Reset failed login attempts (called after successful login)
    /// </summary>
    public void ResetFailedLoginAttempts()
    {
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }
}
