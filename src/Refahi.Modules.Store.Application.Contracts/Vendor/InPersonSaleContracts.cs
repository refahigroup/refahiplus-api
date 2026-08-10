using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Vendor;

public sealed record InPersonOrderDto(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string PaymentState,
    long AmountMinor,
    string? MaskedMobileNumber,
    string? OtpReferenceCode,
    DateTimeOffset? OtpExpiresAt,
    Guid? ProductId = null,
    string? ProductTitle = null,
    long? GrossAmountMinor = null,
    decimal? CommissionPercent = null,
    long? CommissionAmountMinor = null,
    decimal? VatPercent = null,
    long? VatAmountMinor = null,
    long? VendorNetAmountMinor = null,
    Guid? StoreOrderId = null,
    string? CheckoutDestination = null
);

public sealed record InPersonProductDto(
    Guid ProductId,
    string Title,
    string? CategoryName,
    Guid AgreementProductId,
    int CategoryId = 0,
    Guid? AgreementId = null,
    Guid? AgreementCategoryTermId = null,
    decimal? CommissionPercent = null
);

public sealed record InPersonShopDto(Guid ShopId, string Name, string Slug, Guid SupplierId);

public sealed record GetInPersonProductsQuery(Guid VendorUserId, Guid ShopId)
    : IRequest<IReadOnlyList<InPersonProductDto>>;

public sealed record GetUserInPersonShopsQuery(Guid UserId)
    : IRequest<IReadOnlyList<InPersonShopDto>>;

public sealed record GetUserInPersonProductsQuery(Guid UserId, Guid ShopId)
    : IRequest<IReadOnlyList<InPersonProductDto>>;

public sealed record VendorStoreOrderItemSnapshotDto(
    Guid StoreOrderItemId,
    Guid ProductId,
    Guid? OfferId,
    string ProductTitle,
    int CategoryId,
    string CategoryCode,
    Guid SupplierId,
    Guid ShopId,
    string SalesChannel,
    string ProductType,
    string SalesModel,
    string FulfillmentMethod,
    int Quantity,
    long UnitPriceMinor,
    long GrossAmountMinor,
    long? DeclaredGrossAmountMinor,
    Guid AgreementId,
    Guid AgreementCategoryTermId,
    decimal CommissionPercent,
    long CommissionAmountMinor
);

public sealed record VendorStoreOrderSnapshotDto(
    Guid StoreOrderId,
    Guid OrderId,
    Guid UserId,
    Guid CreatedByUserId,
    string SalesChannel,
    string InitiatorType,
    string Status,
    Guid SupplierId,
    Guid ShopId,
    long FinalAmountMinor,
    long? DeclaredGrossAmountMinor,
    IReadOnlyList<VendorStoreOrderItemSnapshotDto> Items
);

public sealed record GetVendorStoreOrderByOrderIdQuery(Guid VendorUserId, Guid OrderId)
    : IRequest<VendorStoreOrderSnapshotDto?>;

public sealed record GetVendorStoreOrdersByOrderIdsQuery(
    Guid VendorUserId,
    IReadOnlyList<Guid> OrderIds
) : IRequest<IReadOnlyList<VendorStoreOrderSnapshotDto>>;

public sealed record GetUserStoreOrderByOrderIdQuery(
    Guid UserId,
    Guid OrderId,
    bool IsAdmin = false
) : IRequest<VendorStoreOrderSnapshotDto?>;

public sealed record GetUserStoreOrdersByOrderIdsQuery(
    Guid UserId,
    IReadOnlyList<Guid> OrderIds,
    bool IsAdmin = false
) : IRequest<IReadOnlyList<VendorStoreOrderSnapshotDto>>;

public sealed record InPersonOtpReference(
    Guid OrderId,
    Guid ShopId,
    string MobileNumber,
    string ProviderReferenceCode
);

public interface IInPersonOtpReferenceProtector
{
    string Protect(InPersonOtpReference reference);
    bool TryUnprotect(string protectedReference, out InPersonOtpReference? reference);
}

public sealed record StartInPersonOrderCommand(
    Guid VendorUserId,
    Guid ShopId,
    Guid ProductId,
    string MobileNumber,
    long AmountMinor,
    string IdempotencyKey
) : IRequest<InPersonOrderDto>;

public sealed record StartUserInPersonOrderCommand(
    Guid UserId,
    Guid ShopId,
    Guid ProductId,
    long AmountMinor,
    string IdempotencyKey
) : IRequest<InPersonOrderDto>;

public sealed record VerifyInPersonOrderCommand(
    Guid VendorUserId,
    Guid OrderId,
    string OtpReferenceCode,
    string OtpCode,
    string IdempotencyKey
) : IRequest<InPersonOrderDto>;

public sealed record ResendInPersonOrderOtpCommand(Guid VendorUserId, Guid OrderId)
    : IRequest<InPersonOrderDto>;

public sealed record CancelInPersonOrderCommand(
    Guid VendorUserId,
    Guid OrderId,
    string IdempotencyKey
) : IRequest<InPersonOrderDto>;
