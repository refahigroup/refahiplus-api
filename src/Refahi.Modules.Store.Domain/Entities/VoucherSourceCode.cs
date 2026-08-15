using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VoucherSourceCode
{
    private VoucherSourceCode() { }

    public Guid Id { get; private set; }
    public Guid VoucherSourceId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string CodeCiphertext { get; private set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public VoucherSourceCodeStatus Status { get; private set; }
    public uint Version { get; private set; }

    public static VoucherSourceCode Register(
        Guid sourceId,
        Guid supplierId,
        string codeHash,
        string codeCiphertext,
        DateTimeOffset registeredAtUtc,
        DateTimeOffset? expiresAtUtc)
    {
        if (sourceId == Guid.Empty || supplierId == Guid.Empty
            || string.IsNullOrWhiteSpace(codeHash) || string.IsNullOrWhiteSpace(codeCiphertext))
            throw new StoreDomainException("اطلاعات کد منبع معتبر نیست", "INVALID_VOUCHER_SOURCE_CODE");
        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= registeredAtUtc)
            throw new StoreDomainException("تاریخ انقضای کد باید در آینده باشد", "INVALID_VOUCHER_CODE_EXPIRY");
        return new VoucherSourceCode
        {
            Id = Guid.NewGuid(),
            VoucherSourceId = sourceId,
            SupplierId = supplierId,
            CodeHash = codeHash,
            CodeCiphertext = codeCiphertext,
            RegisteredAtUtc = registeredAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            Status = VoucherSourceCodeStatus.Available,
        };
    }

    public bool IsAvailableAt(DateTimeOffset requiredUntilUtc) =>
        Status == VoucherSourceCodeStatus.Available
        && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > requiredUntilUtc);

    public void Reserve()
    {
        if (Status != VoucherSourceCodeStatus.Available)
            throw new StoreDomainException("کد ووچر قابل رزرو نیست", "VOUCHER_CODE_NOT_AVAILABLE");
        Status = VoucherSourceCodeStatus.Reserved;
    }

    public void Assign()
    {
        if (Status == VoucherSourceCodeStatus.Assigned)
            return;
        if (Status != VoucherSourceCodeStatus.Reserved)
            throw new StoreDomainException("کد ووچر رزرو نشده است", "VOUCHER_CODE_NOT_RESERVED");
        Status = VoucherSourceCodeStatus.Assigned;
    }

    public void Release()
    {
        if (Status != VoucherSourceCodeStatus.Reserved)
            throw new StoreDomainException("فقط کد رزروشده قابل آزادسازی است", "VOUCHER_CODE_NOT_RELEASABLE");
        Status = VoucherSourceCodeStatus.Available;
    }

    public void Disable()
    {
        if (Status != VoucherSourceCodeStatus.Available)
            throw new StoreDomainException("فقط کد آزاد قابل غیرفعال‌سازی است", "VOUCHER_CODE_NOT_DISABLEABLE");
        Status = VoucherSourceCodeStatus.Disabled;
    }
}
