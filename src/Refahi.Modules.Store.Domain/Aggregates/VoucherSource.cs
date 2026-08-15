using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class VoucherSource
{
    private VoucherSource() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public VoucherSourceType SourceType { get; private set; }
    public VoucherRedemptionMode RedemptionMode { get; private set; }
    public int? DefaultValidityDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static VoucherSource Create(
        Guid supplierId,
        string title,
        VoucherSourceType sourceType,
        VoucherRedemptionMode redemptionMode,
        int? defaultValidityDays,
        DateTimeOffset nowUtc)
    {
        Validate(supplierId, title, sourceType, redemptionMode, defaultValidityDays);
        return new VoucherSource
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Title = title.Trim(),
            SourceType = sourceType,
            RedemptionMode = redemptionMode,
            DefaultValidityDays = sourceType == VoucherSourceType.Generated
                ? defaultValidityDays
                : null,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void Update(
        string title,
        VoucherRedemptionMode redemptionMode,
        int? defaultValidityDays,
        DateTimeOffset nowUtc)
    {
        Validate(SupplierId, title, SourceType, redemptionMode, defaultValidityDays);
        Title = title.Trim();
        RedemptionMode = redemptionMode;
        DefaultValidityDays = SourceType == VoucherSourceType.Generated
            ? defaultValidityDays
            : null;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        IsActive = true;
        UpdatedAtUtc = nowUtc;
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        IsActive = false;
        UpdatedAtUtc = nowUtc;
    }

    private static void Validate(
        Guid supplierId,
        string title,
        VoucherSourceType sourceType,
        VoucherRedemptionMode redemptionMode,
        int? defaultValidityDays)
    {
        if (supplierId == Guid.Empty || string.IsNullOrWhiteSpace(title))
            throw new StoreDomainException("اطلاعات منبع ووچر معتبر نیست", "INVALID_VOUCHER_SOURCE");
        if (!Enum.IsDefined(sourceType) || !Enum.IsDefined(redemptionMode))
            throw new StoreDomainException("نوع منبع یا اعتبارسنجی ووچر معتبر نیست", "INVALID_VOUCHER_SOURCE");
        if (sourceType == VoucherSourceType.Preloaded && defaultValidityDays.HasValue)
            throw new StoreDomainException("مدت اعتبار پیش‌فرض فقط برای منبع تولیدی مجاز است", "INVALID_VOUCHER_SOURCE_VALIDITY");
        if (defaultValidityDays is <= 0 or > 3650)
            throw new StoreDomainException("مدت اعتبار منبع ووچر معتبر نیست", "INVALID_VOUCHER_SOURCE_VALIDITY");
    }
}
