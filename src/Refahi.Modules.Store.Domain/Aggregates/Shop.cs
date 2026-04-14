using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Shop
{
    private Shop() { _products = new List<Product>(); }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;           // URL-friendly unique
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public ShopType ShopType { get; private set; }
    public ShopStatus Status { get; private set; }
    public Guid ProviderId { get; private set; }                       // FK → Providers (via Contract)
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? Description { get; private set; }
    public string? ContactPhone { get; private set; }
    public bool IsPopular { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Product> _products;
    public IReadOnlyList<Product> Products => _products.AsReadOnly();

    // --- Factory ---
    public static Shop Create(
        string name, string slug, ShopType shopType, Guid providerId,
        string? city = null, string? address = null,
        string? description = null, string? contactPhone = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new StoreDomainException("نام فروشگاه الزامی است", "SHOP_NAME_REQUIRED");

        return new Shop
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLower(),
            ShopType = shopType,
            Status = ShopStatus.PendingApproval,
            ProviderId = providerId,
            City = city,
            Address = address,
            Description = description,
            ContactPhone = contactPhone,
            IsPopular = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // --- Behaviors ---
    public void Approve() { Status = ShopStatus.Active; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Suspend() { Status = ShopStatus.Suspended; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Close() { Status = ShopStatus.Closed; UpdatedAt = DateTimeOffset.UtcNow; }
    public void SetPopular(bool isPopular) { IsPopular = isPopular; UpdatedAt = DateTimeOffset.UtcNow; }

    public void UpdateInfo(string name, string? description, string? city,
        string? address, string? contactPhone, string? logoUrl, string? coverImageUrl)
    {
        Name = name.Trim();
        Description = description;
        City = city;
        Address = address;
        ContactPhone = contactPhone;
        LogoUrl = logoUrl;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
