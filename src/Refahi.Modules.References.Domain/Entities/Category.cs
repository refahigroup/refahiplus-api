using Refahi.Modules.References.Domain.Exceptions;

namespace Refahi.Modules.References.Domain.Entities;

public sealed class Category
{
    private Category() { _children = new List<Category>(); }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string CategoryCode { get; private set; } = string.Empty;   // e.g. "store.clothing"
    public string? ImageUrl { get; private set; }
    public int? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Category> _children;
    public IReadOnlyList<Category> Children => _children.AsReadOnly();

    public static Category Create(
        string name, string slug, string categoryCode,
        string? imageUrl = null, int? parentId = null, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام دسته‌بندی الزامی است", "CATEGORY_NAME_REQUIRED");
        if (string.IsNullOrWhiteSpace(slug))
            throw new ReferencesDomainException("اسلاگ دسته‌بندی الزامی است", "CATEGORY_SLUG_REQUIRED");
        if (string.IsNullOrWhiteSpace(categoryCode))
            throw new ReferencesDomainException("کد دسته‌بندی الزامی است", "CATEGORY_CODE_REQUIRED");

        return new Category
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            CategoryCode = categoryCode.Trim().ToLowerInvariant(),
            ImageUrl = imageUrl,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    // NEVER delete — فقط Inactive
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public void UpdateInfo(string name, string? imageUrl, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ReferencesDomainException("نام دسته‌بندی الزامی است", "CATEGORY_NAME_REQUIRED");

        Name = name.Trim();
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
    }
}
