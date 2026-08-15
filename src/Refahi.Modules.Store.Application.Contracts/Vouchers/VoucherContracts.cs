using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Vouchers;

public sealed record VoucherDto(
    Guid Id,
    Guid StoreOrderId,
    Guid StoreOrderItemId,
    int SequenceNumber,
    Guid OrderId,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    Guid ShopId,
    string ShopName,
    Guid ProductId,
    string ProductTitle,
    string Code,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? RedeemedAtUtc,
    Guid? RedeemedShopId,
    string? RedeemedShopName,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? ExpiresAtUtc
)
{
    public Guid? VoucherSourceId { get; init; }
    public string? VoucherSourceTitle { get; init; }
    public string? SourceType { get; init; }
    public string? RedemptionMode { get; init; }
}

public sealed record VoucherAuditDto(
    Guid Id,
    Guid StoreOrderId,
    Guid StoreOrderItemId,
    int SequenceNumber,
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    Guid SupplierId,
    string SupplierName,
    Guid ShopId,
    string ShopName,
    Guid ProductId,
    string ProductTitle,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? RedeemedAtUtc,
    Guid? RedeemedByUserId,
    Guid? RedeemedShopId,
    string? RedeemedShopName,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason,
    DateTimeOffset? ExpiresAtUtc
)
{
    public Guid? VoucherSourceId { get; init; }
    public string? VoucherSourceTitle { get; init; }
    public string? SourceType { get; init; }
    public string? RedemptionMode { get; init; }
}

public sealed record VoucherRedemptionDto(
    Guid VoucherId,
    Guid StoreOrderId,
    Guid ProductId,
    string ProductTitle,
    Guid VendorUserId,
    Guid SupplierId,
    Guid ShopId,
    DateTimeOffset RedeemedAtUtc
);

public sealed record VoucherRedemptionHistoryItemDto(
    Guid VoucherId,
    string MaskedReference,
    Guid StoreOrderId,
    Guid ProductId,
    string ProductTitle,
    Guid SupplierId,
    Guid ShopId,
    string ShopName,
    Guid RedeemedByUserId,
    DateTimeOffset RedeemedAtUtc
);

public sealed record VoucherRedemptionHistoryPageDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<VoucherRedemptionHistoryItemDto> Items
);

public sealed record RedeemVoucherCommand(
    Guid VendorUserId,
    Guid ShopId,
    string Code,
    string IdempotencyKey
) : IRequest<VoucherRedemptionDto>;

public sealed record GetMyVouchersQuery(Guid UserId) : IRequest<IReadOnlyList<VoucherDto>>;

public sealed record GetMyVoucherQuery(Guid UserId, Guid VoucherId) : IRequest<VoucherDto?>;

public sealed record GetVendorVoucherRedemptionHistoryQuery(
    Guid VendorUserId,
    Guid SupplierId,
    Guid? ShopId,
    int Page = 1,
    int PageSize = 20
) : IRequest<VoucherRedemptionHistoryPageDto>;

public sealed record GetAdminVoucherAuditQuery(Guid? StoreOrderId, Guid? VoucherId)
    : IRequest<IReadOnlyList<VoucherAuditDto>>;

public sealed record PrepareStoreOrderRefundCommand(
    Guid OrderId,
    string Reason,
    Guid? VoucherRefundOverrideId = null
) : IRequest<PrepareStoreOrderRefundResponse>;

public sealed record PrepareStoreOrderRefundResponse(Guid StoreOrderId, int RevokedCount);

public sealed record AdminStoreOrderRefundDto(
    Guid StoreOrderId,
    Guid OrderId,
    string StoreOrderStatus,
    long FinalAmountMinor,
    bool RefundBlockedByRedeemedVoucher,
    string? RefundBlockerCode,
    IReadOnlyList<VoucherAuditDto> Vouchers,
    VoucherRefundOverrideDto? RefundOverride
);

public sealed record VoucherRefundOverrideDto(
    Guid Id,
    Guid StoreOrderId,
    Guid OrderId,
    Guid AdminUserId,
    string Reason,
    IReadOnlyList<VoucherRefundSnapshotItemDto> VoucherSnapshot,
    DateTimeOffset CreatedAtUtc,
    Guid CorrelationId,
    string Outcome,
    IReadOnlyList<VoucherRefundOverrideAttemptDto> Attempts
);

public sealed record VoucherRefundSnapshotItemDto(
    Guid VoucherId,
    string Status,
    string? RedemptionMode = null);

public sealed record VoucherRefundOverrideAttemptDto(
    int SequenceNumber,
    string Outcome,
    string? PaymentAction,
    string? FailureCode,
    string? FailureMessage,
    DateTimeOffset CreatedAtUtc
);

public sealed record GetAdminStoreOrderRefundQuery(Guid OrderId)
    : IRequest<AdminStoreOrderRefundDto?>;

public sealed record OverrideRedeemedVoucherRefundCommand(
    Guid OrderId,
    Guid AdminUserId,
    string Reason,
    string IdempotencyKey
) : IRequest<VoucherRefundOverrideDto>;

public sealed record VoucherErrorResponse(
    bool Success,
    string Code,
    string Message,
    int StatusCode
);

public interface IVoucherCodeProtector
{
    string Protect(string plaintextCode);
    bool TryUnprotect(string ciphertext, out string plaintextCode);
}

public sealed class VoucherApplicationException : Exception
{
    public VoucherApplicationException(string code, string message)
        : base(message) => Code = code;

    public string Code { get; }
}
