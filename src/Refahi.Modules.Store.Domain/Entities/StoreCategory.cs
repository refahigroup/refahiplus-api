namespace Refahi.Modules.Store.Domain.Entities;

public sealed class StoreCategory
{
    private StoreCategory() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;            // "مد و پوشاک"
    public string Slug { get; private set; } = string.Empty;
    public string CategoryCode { get; private set; } = string.Empty;   // "store.clothing"
    public string? ImageUrl { get; private set; }
    public int? ParentId { get; private set; }                          // Self-referencing
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static StoreCategory Create(
        string name, string slug, string categoryCode,
        string? imageUrl = null, int? parentId = null, int sortOrder = 0)
        => new()
        {
            Name = name,
            Slug = slug.ToLower(),
            CategoryCode = categoryCode,
            ImageUrl = imageUrl,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsActive = true
        };

    // NEVER delete — فقط Inactive
    public void Deactivate() { IsActive = false; }
    public void Activate() { IsActive = true; }

    public void UpdateInfo(string name, string? imageUrl, int sortOrder)
    {
        Name = name.Trim();
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
    }
}
