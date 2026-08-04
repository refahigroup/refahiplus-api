using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Vendor;

public sealed record InPersonOrderDto(
    Guid OrderId, string OrderNumber, string Status, string PaymentState,
    long AmountMinor, string? MaskedMobileNumber,
    string? OtpReferenceCode, DateTimeOffset? OtpExpiresAt,
    Guid? ProductId = null, string? ProductTitle = null,
    long? GrossAmountMinor = null, decimal? CommissionPercent = null,
    long? CommissionAmountMinor = null, decimal? VatPercent = null,
    long? VatAmountMinor = null, long? VendorNetAmountMinor = null);

public sealed record InPersonProductDto(
    Guid ProductId, string Title, string? CategoryName, Guid AgreementProductId);

public sealed record GetInPersonProductsQuery(Guid VendorUserId, Guid ShopId)
    : IRequest<IReadOnlyList<InPersonProductDto>>;

public sealed record InPersonOtpReference(
    Guid OrderId, Guid ShopId, string MobileNumber, string ProviderReferenceCode);

public interface IInPersonOtpReferenceProtector
{
    string Protect(InPersonOtpReference reference);
    bool TryUnprotect(string protectedReference, out InPersonOtpReference? reference);
}

public sealed record StartInPersonOrderCommand(
    Guid VendorUserId, Guid ShopId, Guid ProductId, string MobileNumber,
    long AmountMinor, string IdempotencyKey) : IRequest<InPersonOrderDto>;

public sealed record VerifyInPersonOrderCommand(
    Guid VendorUserId, Guid OrderId, string OtpReferenceCode,
    string OtpCode, string IdempotencyKey) : IRequest<InPersonOrderDto>;

public sealed record ResendInPersonOrderOtpCommand(Guid VendorUserId, Guid OrderId)
    : IRequest<InPersonOrderDto>;

public sealed record CancelInPersonOrderCommand(Guid VendorUserId, Guid OrderId, string IdempotencyKey)
    : IRequest<InPersonOrderDto>;
