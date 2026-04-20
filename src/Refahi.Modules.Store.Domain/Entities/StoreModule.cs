namespace Refahi.Modules.Store.Domain.Entities;

public sealed class StoreModule
{
    private static readonly HashSet<string> ReservedSlugs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "orders", "wallets", "identity", "admin", "api",
            "modules", "provider", "ping"
        };

    private StoreModule() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;        // URL segment — unique, e.g. "store", "hotel"
    public string? Description { get; private set; }
    public string? IconUrl { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    public static StoreModule Create(
        string name, string slug,
        string? description = null, string? iconUrl = null, int sortOrder = 0)
    {
        var normalizedSlug = slug.Trim().ToLower();
        if (ReservedSlugs.Contains(normalizedSlug))
            throw new Exceptions.StoreDomainException(
                $"اسلاگ '{normalizedSlug}' رزرو شده است و نمی‌توان از آن استفاده کرد",
                "RESERVED_MODULE_SLUG");

        return new StoreModule
        {
            Name = name.Trim(),
            Slug = normalizedSlug,
            Description = description,
            IconUrl = iconUrl,
            IsActive = true,
            SortOrder = sortOrder
        };
    }

    public void Activate() { IsActive = true; }
    public void Deactivate() { IsActive = false; }

    public void UpdateInfo(string name, string? description, string? iconUrl, int sortOrder)
    {
        Name = name.Trim();
        Description = description;
        IconUrl = iconUrl;
        SortOrder = sortOrder;
    }
}
