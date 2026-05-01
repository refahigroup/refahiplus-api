using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Domain.Aggregates;

public sealed class Supplier
{
    private Supplier()
    {
        _links = new List<SupplierLink>();
        _attachments = new List<SupplierAttachment>();
    }

    public Guid Id { get; private set; }
    public SupplierType Type { get; private set; }

    // Individual fields
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }

    // Legal/common fields
    public string? CompanyName { get; private set; }
    public string? BrandName { get; private set; }
    public string? LogoUrl { get; private set; }

    public string? NationalId { get; private set; }
    public string? EconomicCode { get; private set; }

    // Location (FK IDs only — no EF navigation across modules)
    public int? ProvinceId { get; private set; }
    public int? CityId { get; private set; }
    public string? Address { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    // Contact
    public string? MobileNumber { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? RepresentativeName { get; private set; }
    public string? RepresentativePhone { get; private set; }

    public SupplierStatus Status { get; private set; }
    public string? StatusNote { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<SupplierLink> _links;
    public IReadOnlyList<SupplierLink> Links => _links.AsReadOnly();

    private readonly List<SupplierAttachment> _attachments;
    public IReadOnlyList<SupplierAttachment> Attachments => _attachments.AsReadOnly();

    // --- Factory ---
    public static Supplier Create(
        SupplierType type,
        string? firstName, string? lastName,
        string? companyName, string? brandName,
        string? logoUrl,
        string? nationalId, string? economicCode,
        int? provinceId, int? cityId,
        string? address, double? latitude, double? longitude,
        string? mobileNumber, string? phoneNumber,
        string? representativeName, string? representativePhone)
    {
        if (type == SupplierType.Individual && (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)))
            throw new SupplyChainDomainException("نام و نام خانوادگی الزامی است", "SUPPLIER_INDIVIDUAL_NAME_REQUIRED");

        if (type == SupplierType.Legal && string.IsNullOrWhiteSpace(companyName))
            throw new SupplyChainDomainException("نام شرکت الزامی است", "SUPPLIER_COMPANY_NAME_REQUIRED");

        return new Supplier
        {
            Id = Guid.NewGuid(),
            Type = type,
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            CompanyName = companyName?.Trim(),
            BrandName = brandName?.Trim(),
            LogoUrl = logoUrl?.Trim(),
            NationalId = nationalId?.Trim(),
            EconomicCode = economicCode?.Trim(),
            ProvinceId = provinceId,
            CityId = cityId,
            Address = address?.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            MobileNumber = mobileNumber?.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            RepresentativeName = representativeName?.Trim(),
            RepresentativePhone = representativePhone?.Trim(),
            Status = SupplierStatus.Registered,
            StatusNote = null,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // --- Behaviors ---

    public void UpdateProfile(
        string? firstName, string? lastName,
        string? companyName, string? brandName,
        string? logoUrl,
        string? nationalId, string? economicCode,
        int? provinceId, int? cityId,
        string? address, double? latitude, double? longitude,
        string? mobileNumber, string? phoneNumber,
        string? representativeName, string? representativePhone)
    {
        if (Status != SupplierStatus.Registered && Status != SupplierStatus.Rejected)
            throw new SupplyChainDomainException("ویرایش پروفایل تنها در وضعیت ثبت‌شده یا رد‌شده مجاز است", "STATUS_IMMUTABLE");

        if (Type == SupplierType.Individual && (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)))
            throw new SupplyChainDomainException("نام و نام خانوادگی الزامی است", "SUPPLIER_INDIVIDUAL_NAME_REQUIRED");

        if (Type == SupplierType.Legal && string.IsNullOrWhiteSpace(companyName))
            throw new SupplyChainDomainException("نام شرکت الزامی است", "SUPPLIER_COMPANY_NAME_REQUIRED");

        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        CompanyName = companyName?.Trim();
        BrandName = brandName?.Trim();
        LogoUrl = logoUrl?.Trim();
        NationalId = nationalId?.Trim();
        EconomicCode = economicCode?.Trim();
        ProvinceId = provinceId;
        CityId = cityId;
        Address = address?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        MobileNumber = mobileNumber?.Trim();
        PhoneNumber = phoneNumber?.Trim();
        RepresentativeName = representativeName?.Trim();
        RepresentativePhone = representativePhone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SubmitForReview()
    {
        if (Status != SupplierStatus.Registered)
            throw new SupplyChainDomainException("ارسال برای بررسی تنها از وضعیت ثبت‌شده مجاز است", "INVALID_STATUS_TRANSITION");

        Status = SupplierStatus.UnderReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve()
    {
        if (Status != SupplierStatus.UnderReview)
            throw new SupplyChainDomainException("تایید تنها از وضعیت در حال بررسی مجاز است", "INVALID_STATUS_TRANSITION");

        Status = SupplierStatus.Approved;
        StatusNote = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(string note)
    {
        if (Status != SupplierStatus.UnderReview)
            throw new SupplyChainDomainException("رد کردن تنها از وضعیت در حال بررسی مجاز است", "INVALID_STATUS_TRANSITION");

        Status = SupplierStatus.Rejected;
        StatusNote = note;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetToRegistered()
    {
        if (Status != SupplierStatus.UnderReview && Status != SupplierStatus.Rejected)
            throw new SupplyChainDomainException("بازگشت به وضعیت ثبت‌شده تنها از وضعیت در حال بررسی یا رد‌شده مجاز است", "INVALID_STATUS_TRANSITION");

        Status = SupplierStatus.Registered;
        StatusNote = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public SupplierLink AddLink(SupplierLinkType type, string url, string? label)
    {
        var link = SupplierLink.Create(Id, type, url, label);
        _links.Add(link);
        UpdatedAt = DateTimeOffset.UtcNow;
        return link;
    }

    public void RemoveLink(Guid linkId)
    {
        var link = _links.FirstOrDefault(l => l.Id == linkId)
            ?? throw new SupplyChainDomainException("لینک یافت نشد", "SUPPLIER_LINK_NOT_FOUND");

        _links.Remove(link);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public SupplierAttachment AddAttachment(string title, string fileUrl, string? fileName, string? contentType, long? sizeBytes)
    {
        var attachment = SupplierAttachment.Create(Id, title, fileUrl, fileName, contentType, sizeBytes);
        _attachments.Add(attachment);
        UpdatedAt = DateTimeOffset.UtcNow;
        return attachment;
    }

    public void RemoveAttachment(Guid attachmentId)
    {
        var attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new SupplyChainDomainException("پیوست یافت نشد", "SUPPLIER_ATTACHMENT_NOT_FOUND");

        _attachments.Remove(attachment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        if (IsDeleted)
            throw new SupplyChainDomainException("تامین‌کننده قبلاً حذف شده است", "SUPPLIER_ALREADY_DELETED");

        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
