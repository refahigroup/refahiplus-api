using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Domain.Entities;

/// <summary>
/// UserProfile entity - contains personal information about the user
/// </summary>
public class UserProfile
{
    // Private constructor for EF
    private UserProfile() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? NationalCode { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public DateOnly? Birthday { get; private set; }
    public Gender? Gender { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    /// <summary>
    /// Factory method to create a new profile
    /// </summary>
    public static UserProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        string? nationalCode = null,
        Gender? gender = null,
        DateOnly? birthday = null,
        string? profileImageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required", "FIRSTNAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required", "LASTNAME_REQUIRED");

        if (!string.IsNullOrWhiteSpace(nationalCode) && nationalCode.Length != 10)
            throw new DomainException("National code must be 10 digits", "INVALID_NATIONAL_CODE");

        return new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Birthday = birthday,
            ProfileImageUrl = profileImageUrl?.Trim(),
            NationalCode = nationalCode?.Trim(),
            Gender = gender,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update profile information
    /// </summary>
    public void Update(
        string firstName,
        string lastName,
        string? nationalCode = null,
        Gender? gender = null,
        DateOnly? birthday = null,
        string? profileImageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required", "FIRSTNAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required", "LASTNAME_REQUIRED");

        if (!string.IsNullOrWhiteSpace(nationalCode) && nationalCode.Length != 10)
            throw new DomainException("National code must be 10 digits", "INVALID_NATIONAL_CODE");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        NationalCode = nationalCode?.Trim();
        Gender = gender;
        Birthday = birthday;
        ProfileImageUrl = profileImageUrl?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get full name
    /// </summary>
    public string GetFullName()
    {
        return $"{FirstName} {LastName}";
    }
    public void SetProfileImage(string? imageUrl)
    {
        ProfileImageUrl = imageUrl?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Set birthday
    /// </summary>
    public void SetBirthday(DateOnly? birthday)
    {
        Birthday = birthday;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculate age from birthday
    /// </summary>
    public int? GetAge()
    {
        if (!Birthday.HasValue)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - Birthday.Value.Year;

        // Adjust if birthday hasn't occurred this year yet
        if (Birthday.Value > today.AddYears(-age))
            age--;

        return age;
    }
}
