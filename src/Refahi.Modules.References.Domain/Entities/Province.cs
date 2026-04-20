using Refahi.Modules.References.Domain.Exceptions;

namespace Refahi.Modules.References.Domain.Entities;

/// <summary>
/// استان — Province entity
/// </summary>
public sealed class Province
{
    private Province() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Factory method for creating a new Province
    /// </summary>
    public static Province Create(string name, string slug, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام استان الزامی است", "PROVINCE_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(slug))
            throw new ReferencesDomainException("اسلاگ استان الزامی است", "PROVINCE_SLUG_REQUIRED");

        return new Province
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    /// <summary>
    /// Activate province
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivate province
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Update province info
    /// </summary>
    public void UpdateInfo(string name, string slug, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام استان الزامی است", "PROVINCE_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(slug))
            throw new ReferencesDomainException("اسلاگ استان الزامی است", "PROVINCE_SLUG_REQUIRED");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        SortOrder = sortOrder;
    }
}
