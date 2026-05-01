using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class Shop
{
    private Shop() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;           // URL-friendly unique
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public ShopType ShopType { get; private set; }
    public ShopStatus Status { get; private set; }
    public Guid SupplierId { get; private set; }
    
    // Location (replaced string City with CityId + ProvinceId FKs)
    public int? ProvinceId { get; private set; }                       // FK → References.Province
    public int? CityId { get; private set; }                           // FK → References.City
    public string? Address { get; private set; }
    public double? Latitude { get; private set; }                      // GPS latitude
    public double? Longitude { get; private set; }                     // GPS longitude
    
    // Contact info
    public string? ManagerName { get; private set; }
    public string? ManagerPhone { get; private set; }
    public string? RepresentativeName { get; private set; }
    public string? RepresentativePhone { get; private set; }
    public string? ContactPhone { get; private set; }
    
    public string? Description { get; private set; }
    public bool IsPopular { get; private set; }
    public int DeliveredOrdersCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // --- Factory ---
    public static Shop Create(
        string name, string slug, ShopType shopType, Guid supplierId,
        int? provinceId = null, int? cityId = null,
        string? address = null, double? latitude = null, double? longitude = null,
        string? managerName = null, string? managerPhone = null,
        string? representativeName = null, string? representativePhone = null,
        string? contactPhone = null, string? description = null)
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
            SupplierId = supplierId,
            ProvinceId = provinceId,
            CityId = cityId,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            ManagerName = managerName,
            ManagerPhone = managerPhone,
            RepresentativeName = representativeName,
            RepresentativePhone = representativePhone,
            ContactPhone = contactPhone,
            Description = description,
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
    public void RecordDelivery() { DeliveredOrdersCount++; UpdatedAt = DateTimeOffset.UtcNow; }

    public void UpdateInfo(string name, string? description,
        int? provinceId, int? cityId, string? address,
        double? latitude, double? longitude,
        string? managerName, string? managerPhone,
        string? representativeName, string? representativePhone,
        string? contactPhone, string? logoUrl, string? coverImageUrl)
    {
        Name = name.Trim();
        Description = description;
        ProvinceId = provinceId;
        CityId = cityId;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        ManagerName = managerName;
        ManagerPhone = managerPhone;
        RepresentativeName = representativeName;
        RepresentativePhone = representativePhone;
        ContactPhone = contactPhone;
        LogoUrl = logoUrl;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
