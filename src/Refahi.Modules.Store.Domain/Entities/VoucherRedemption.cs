using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VoucherRedemption
{
    private VoucherRedemption() { }

    public Guid Id { get; private set; }
    public Guid VoucherId { get; private set; }
    public Guid VendorUserId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid ShopId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset RedeemedAtUtc { get; private set; }

    public static VoucherRedemption Create(Guid voucherId, Guid vendorUserId, Guid supplierId,
        Guid shopId, string idempotencyKey, string requestFingerprint, DateTimeOffset redeemedAtUtc)
    {
        if (voucherId == Guid.Empty || vendorUserId == Guid.Empty || supplierId == Guid.Empty ||
            shopId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey) ||
            string.IsNullOrWhiteSpace(requestFingerprint))
            throw new StoreDomainException("اطلاعات ثبت استفاده ووچر معتبر نیست", "INVALID_VOUCHER_REDEMPTION");
        return new VoucherRedemption
        {
            Id = Guid.NewGuid(), VoucherId = voucherId, VendorUserId = vendorUserId,
            SupplierId = supplierId, ShopId = shopId, IdempotencyKey = idempotencyKey.Trim(),
            RequestFingerprint = requestFingerprint, RedeemedAtUtc = redeemedAtUtc
        };
    }
}
