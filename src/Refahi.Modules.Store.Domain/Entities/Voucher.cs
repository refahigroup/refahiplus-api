using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class Voucher
{
    private Voucher() { }

    public Guid Id { get; private set; }
    public Guid StoreOrderId { get; private set; }
    public Guid StoreOrderItemId { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public int SequenceNumber { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string SupplierName { get; private set; } = string.Empty;
    public Guid ShopId { get; private set; }
    public string ShopName { get; private set; } = string.Empty;
    public Guid ProductId { get; private set; }
    public string ProductTitle { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public string CodeCiphertext { get; private set; } = string.Empty;
    public VoucherStatus Status { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? RedeemedAtUtc { get; private set; }
    public Guid? RedeemedByUserId { get; private set; }
    public Guid? RedeemedShopId { get; private set; }
    public string? RedeemedShopName { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static Voucher Issue(
        Guid storeOrderId,
        Guid storeOrderItemId,
        Guid orderId,
        string orderNumber,
        int sequenceNumber,
        Guid userId,
        Guid supplierId,
        string supplierName,
        Guid shopId,
        string shopName,
        Guid productId,
        string productTitle,
        string codeHash,
        string codeCiphertext,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset? expiresAtUtc = null
    )
    {
        if (
            storeOrderId == Guid.Empty
            || storeOrderItemId == Guid.Empty
            || orderId == Guid.Empty
            || sequenceNumber <= 0
            || userId == Guid.Empty
            || supplierId == Guid.Empty
            || shopId == Guid.Empty
            || productId == Guid.Empty
            || string.IsNullOrWhiteSpace(orderNumber)
            || string.IsNullOrWhiteSpace(supplierName)
            || string.IsNullOrWhiteSpace(shopName)
            || string.IsNullOrWhiteSpace(productTitle)
            || string.IsNullOrWhiteSpace(codeHash)
            || string.IsNullOrWhiteSpace(codeCiphertext)
        )
            throw new StoreDomainException("اطلاعات صدور ووچر معتبر نیست", "INVALID_VOUCHER_ISSUE");

        return new Voucher
        {
            Id = Guid.NewGuid(),
            StoreOrderId = storeOrderId,
            StoreOrderItemId = storeOrderItemId,
            OrderId = orderId,
            OrderNumber = orderNumber.Trim(),
            SequenceNumber = sequenceNumber,
            UserId = userId,
            SupplierId = supplierId,
            SupplierName = supplierName.Trim(),
            ShopId = shopId,
            ShopName = shopName.Trim(),
            ProductId = productId,
            ProductTitle = productTitle.Trim(),
            CodeHash = codeHash,
            CodeCiphertext = codeCiphertext,
            Status = VoucherStatus.Issued,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    public void Redeem(Guid vendorUserId, Guid shopId, string shopName, DateTimeOffset nowUtc)
    {
        ExpireIfNeeded(nowUtc);
        if (Status != VoucherStatus.Issued)
            throw new StoreDomainException("این ووچر قابل استفاده نیست", "VOUCHER_NOT_REDEEMABLE");
        if (
            vendorUserId == Guid.Empty
            || shopId == Guid.Empty
            || string.IsNullOrWhiteSpace(shopName)
        )
            throw new StoreDomainException(
                "اطلاعات ابطال ووچر معتبر نیست",
                "INVALID_VOUCHER_REDEMPTION"
            );
        Status = VoucherStatus.Redeemed;
        RedeemedAtUtc = nowUtc;
        RedeemedByUserId = vendorUserId;
        RedeemedShopId = shopId;
        RedeemedShopName = shopName.Trim();
    }

    public bool RevokeForRefund(string reason, DateTimeOffset nowUtc)
    {
        if (Status == VoucherStatus.Revoked)
            return false;
        if (Status == VoucherStatus.Redeemed)
            throw new StoreDomainException(
                "به دلیل استفاده شدن ووچر، بازگشت وجه خودکار امکان‌پذیر نیست",
                "REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE"
            );
        if (Status == VoucherStatus.Expired)
            return false;
        if (Status != VoucherStatus.Issued)
            throw new StoreDomainException(
                "وضعیت ووچر برای ابطال معتبر نیست",
                "VOUCHER_REFUND_CONFLICT"
            );
        Status = VoucherStatus.Revoked;
        RevokedAtUtc = nowUtc;
        RevocationReason = string.IsNullOrWhiteSpace(reason)
            ? "ابطال به علت بازگشت وجه سفارش"
            : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        return true;
    }

    public bool ExpireIfNeeded(DateTimeOffset nowUtc)
    {
        if (Status == VoucherStatus.Issued && ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc)
        {
            Status = VoucherStatus.Expired;
            return true;
        }
        return false;
    }
}
