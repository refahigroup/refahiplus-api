using Refahi.Modules.References.Domain.Exceptions;

namespace Refahi.Modules.References.Domain.Entities;

/// <summary>
/// شهر — City entity
/// </summary>
public sealed class City
{
    private City() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public int ProvinceId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation property (EF Core will populate this)
    public Province Province { get; private set; } = null!;

    /// <summary>
    /// Factory method for creating a new City
    /// </summary>
    public static City Create(string name, string slug, int provinceId, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام شهر الزامی است", "CITY_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(slug))
            throw new ReferencesDomainException("اسلاگ شهر الزامی است", "CITY_SLUG_REQUIRED");

        if (provinceId <= 0)
            throw new ReferencesDomainException("شناسه استان نامعتبر است", "INVALID_PROVINCE_ID");

        return new City
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            ProvinceId = provinceId,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    /// <summary>
    /// Activate city
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivate city
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Update city info
    /// </summary>
    public void UpdateInfo(string name, string slug, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام شهر الزامی است", "CITY_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(slug))
            throw new ReferencesDomainException("اسلاگ شهر الزامی است", "CITY_SLUG_REQUIRED");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        SortOrder = sortOrder;
    }
}
