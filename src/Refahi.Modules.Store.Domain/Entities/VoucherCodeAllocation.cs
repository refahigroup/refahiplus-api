using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VoucherCodeAllocation
{
    private VoucherCodeAllocation() { }

    public Guid Id { get; private set; }
    public Guid VoucherSourceCodeId { get; private set; }
    public Guid StoreOrderId { get; private set; }
    public Guid StoreOrderItemId { get; private set; }
    public int SequenceNumber { get; private set; }
    public VoucherCodeAllocationStatus Status { get; private set; }
    public DateTimeOffset ReservedAtUtc { get; private set; }
    public DateTimeOffset ReservedUntilUtc { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public Guid? VoucherId { get; private set; }
    public uint Version { get; private set; }

    public static VoucherCodeAllocation Reserve(
        Guid sourceCodeId,
        Guid storeOrderId,
        Guid storeOrderItemId,
        int sequenceNumber,
        DateTimeOffset nowUtc,
        DateTimeOffset reservedUntilUtc)
    {
        if (sourceCodeId == Guid.Empty || storeOrderId == Guid.Empty
            || storeOrderItemId == Guid.Empty || sequenceNumber <= 0 || reservedUntilUtc <= nowUtc)
            throw new StoreDomainException("اطلاعات رزرو کد معتبر نیست", "INVALID_VOUCHER_CODE_RESERVATION");
        return new VoucherCodeAllocation
        {
            Id = Guid.NewGuid(),
            VoucherSourceCodeId = sourceCodeId,
            StoreOrderId = storeOrderId,
            StoreOrderItemId = storeOrderItemId,
            SequenceNumber = sequenceNumber,
            Status = VoucherCodeAllocationStatus.Reserved,
            ReservedAtUtc = nowUtc,
            ReservedUntilUtc = reservedUntilUtc,
        };
    }

    public void Assign(Guid voucherId, DateTimeOffset nowUtc)
    {
        if (Status == VoucherCodeAllocationStatus.Assigned && VoucherId == voucherId)
            return;
        if (Status != VoucherCodeAllocationStatus.Reserved || voucherId == Guid.Empty)
            throw new StoreDomainException("رزرو کد قابل تخصیص نیست", "VOUCHER_CODE_ALLOCATION_CONFLICT");
        Status = VoucherCodeAllocationStatus.Assigned;
        VoucherId = voucherId;
        AssignedAtUtc = nowUtc;
    }

    public void Release(DateTimeOffset nowUtc)
    {
        if (Status == VoucherCodeAllocationStatus.Released)
            return;
        if (Status != VoucherCodeAllocationStatus.Reserved)
            throw new StoreDomainException("کد تخصیص‌یافته قابل آزادسازی نیست", "VOUCHER_CODE_ALLOCATION_CONFLICT");
        Status = VoucherCodeAllocationStatus.Released;
        ReleasedAtUtc = nowUtc;
    }
}
