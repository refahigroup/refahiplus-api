using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Exceptions;

namespace Refahi.Modules.Identity.Domain.Entities;

/// <summary>
/// آدرس ذخیره‌شده‌ی کاربر — برای استفاده در فلوی خرید (آدرس ارسال).
/// یک کاربر می‌تواند چندین آدرس داشته باشد. حداکثر یکی از آن‌ها به‌عنوان آدرس پیش‌فرض علامت‌گذاری می‌شود.
/// </summary>
public class UserAddress
{
    // Private constructor for EF
    private UserAddress() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>عنوان دلخواه (مثل «خانه»، «محل کار»).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>شناسه استان (FK → References.Province) — به‌صورت soft FK نگه داشته می‌شود.</summary>
    public int ProvinceId { get; private set; }

    /// <summary>شناسه شهر (FK → References.City) — به‌صورت soft FK نگه داشته می‌شود.</summary>
    public int CityId { get; private set; }

    /// <summary>متن کامل آدرس (خیابان، کوچه، ...).</summary>
    public string FullAddress { get; private set; } = string.Empty;

    /// <summary>کد پستی ۱۰ رقمی.</summary>
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>نام تحویل‌گیرنده.</summary>
    public string ReceiverName { get; private set; } = string.Empty;

    /// <summary>شماره تماس تحویل‌گیرنده (۱۱ رقم، ۰۹ شروع).</summary>
    public string ReceiverPhone { get; private set; } = string.Empty;

    /// <summary>پلاک (اختیاری).</summary>
    public string? Plate { get; private set; }

    /// <summary>واحد (اختیاری).</summary>
    public string? Unit { get; private set; }

    /// <summary>عرض جغرافیایی برای نمایش روی نقشه (اختیاری).</summary>
    public double? Latitude { get; private set; }

    /// <summary>طول جغرافیایی برای نمایش روی نقشه (اختیاری).</summary>
    public double? Longitude { get; private set; }

    /// <summary>آیا این آدرس به‌عنوان پیش‌فرض کاربر علامت زده شده؟</summary>
    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    /// <summary>
    /// Factory برای ساخت آدرس جدید.
    /// </summary>
    public static UserAddress Create(
        Guid userId,
        string title,
        int provinceId,
        int cityId,
        string fullAddress,
        string postalCode,
        string receiverName,
        string receiverPhone,
        string? plate = null,
        string? unit = null,
        double? latitude = null,
        double? longitude = null,
        bool isDefault = false)
    {
        ValidateInput(title, provinceId, cityId, fullAddress, postalCode, receiverName, receiverPhone);

        return new UserAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            ProvinceId = provinceId,
            CityId = cityId,
            FullAddress = fullAddress.Trim(),
            PostalCode = postalCode.Trim(),
            ReceiverName = receiverName.Trim(),
            ReceiverPhone = receiverPhone.Trim(),
            Plate = plate?.Trim(),
            Unit = unit?.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// به‌روزرسانی فیلدهای آدرس (به‌جز IsDefault که از طریق <see cref="MarkAsDefault"/> و <see cref="UnmarkAsDefault"/> مدیریت می‌شود).
    /// </summary>
    public void Update(
        string title,
        int provinceId,
        int cityId,
        string fullAddress,
        string postalCode,
        string receiverName,
        string receiverPhone,
        string? plate = null,
        string? unit = null,
        double? latitude = null,
        double? longitude = null)
    {
        ValidateInput(title, provinceId, cityId, fullAddress, postalCode, receiverName, receiverPhone);

        Title = title.Trim();
        ProvinceId = provinceId;
        CityId = cityId;
        FullAddress = fullAddress.Trim();
        PostalCode = postalCode.Trim();
        ReceiverName = receiverName.Trim();
        ReceiverPhone = receiverPhone.Trim();
        Plate = plate?.Trim();
        Unit = unit?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// این آدرس را به‌عنوان پیش‌فرض علامت می‌زند. مسئولیت برداشتن علامت پیش‌فرض از سایر آدرس‌های کاربر بر عهده‌ی Repository/Service است.
    /// </summary>
    public void MarkAsDefault()
    {
        if (IsDefault) return;
        IsDefault = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// علامت پیش‌فرض را برمی‌دارد.
    /// </summary>
    public void UnmarkAsDefault()
    {
        if (!IsDefault) return;
        IsDefault = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ================================================================
    // Validation
    // ================================================================

    private static void ValidateInput(
        string title,
        int provinceId,
        int cityId,
        string fullAddress,
        string postalCode,
        string receiverName,
        string receiverPhone)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("عنوان آدرس الزامی است", "ADDRESS_TITLE_REQUIRED");

        if (provinceId <= 0)
            throw new DomainException("استان نامعتبر است", "INVALID_PROVINCE");

        if (cityId <= 0)
            throw new DomainException("شهر نامعتبر است", "INVALID_CITY");

        if (string.IsNullOrWhiteSpace(fullAddress))
            throw new DomainException("متن آدرس الزامی است", "ADDRESS_TEXT_REQUIRED");

        if (string.IsNullOrWhiteSpace(postalCode) || postalCode.Trim().Length != 10)
            throw new DomainException("کد پستی باید ۱۰ رقم باشد", "INVALID_POSTAL_CODE");

        if (string.IsNullOrWhiteSpace(receiverName))
            throw new DomainException("نام تحویل‌گیرنده الزامی است", "RECEIVER_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(receiverPhone))
            throw new DomainException("شماره تحویل‌گیرنده الزامی است", "RECEIVER_PHONE_REQUIRED");

        var phone = receiverPhone.Trim();
        if (phone.Length != 11 || !phone.StartsWith("09"))
            throw new DomainException("شماره تحویل‌گیرنده نامعتبر است", "INVALID_RECEIVER_PHONE");
    }
}
