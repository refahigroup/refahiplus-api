using MediatR;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Contracts.Features;

public sealed record CreateChargeRequestCommand(
    Guid UserId, ChargeOperator Operator, ChargeServiceType ServiceType,
    string DestinationMobileNumber, string? OriginMobileNumber, string? ProviderProductId,
    long? RequestedAmountMinor, int? PinCategoryId, int PinCount, long ExpectedFinalAmountMinor,
    string IdempotencyKey)
    : IRequest<CreateChargeRequestResponse>;

public sealed record PreviewChargeRequestCommand(
    ChargeOperator Operator, ChargeServiceType ServiceType, string DestinationMobileNumber,
    string? ProviderProductId, long? RequestedAmountMinor, int? PinCategoryId, int PinCount)
    : IRequest<ChargeRequestQuoteResponse>;

public sealed record ChargeRequestQuoteResponse(
    DateTime ExpireAt, long ProviderCostMinor, long MarkupAmountMinor,
    long FinalAmountMinor, string Currency);

public sealed class ChargeQuoteChangedException(ChargeRequestQuoteResponse quote)
    : Exception("قیمت خرید تغییر کرده است. مبلغ جدید را بررسی و دوباره تایید کنید")
{
    public ChargeRequestQuoteResponse Quote { get; } = quote;
}

public sealed record CreateChargeRequestResponse(
    Guid RequestId, string Status, DateTime ExpireAt, long ProviderCostMinor,
    long MarkupAmountMinor, long FinalAmountMinor, string Currency);

public sealed record ConvertChargeRequestToOrderCommand(Guid RequestId, Guid UserId, string IdempotencyKey)
    : IRequest<ConvertChargeRequestToOrderResponse>;
public sealed record ConvertChargeRequestToOrderResponse(Guid RequestId, Guid OrderId, string OrderNumber, long FinalAmountMinor, string Status);

public sealed record GetChargeRequestQuery(Guid RequestId, Guid UserId, bool IsAdmin = false)
    : IRequest<ChargeRequestDetailDto?>;
public sealed record ChargePinDeliveryDto(string Serial, string Code, long AmountMinor);
public sealed record GetChargeRequestPinsQuery(Guid RequestId, Guid UserId)
    : IRequest<IReadOnlyList<ChargePinDeliveryDto>?>;
public sealed record CancelChargeRequestCommand(Guid RequestId, Guid UserId) : IRequest<bool>;
public sealed record ChargeOperationResponse(Guid Id);
public sealed record ChargeRequestDetailDto(
    Guid RequestId, Guid? OrderId, string Status, ChargeOperator Operator, ChargeServiceType ServiceType,
    string DestinationMobileNumber, string ProductCaption, long ProviderCostMinor, long MarkupAmountMinor,
    long FinalAmountMinor, string? ProviderRrn, string? ProviderTraceId, string? Message,
    DateTime CreatedAt, DateTime? FulfilledAt, int PinCount);

public sealed record GetOperatorsQuery : IRequest<IReadOnlyList<ChargeOperatorDto>>;
public sealed record ChargeServiceCapabilityDto(
    ChargeServiceType ServiceType, bool IsSupported, string? UnavailableReason,
    long? MinimumAmountMinor, long? MaximumAmountMinor, IReadOnlyList<long> SuggestedAmountsMinor);
public sealed record ChargeOperatorDto(short Id, string Code, string PersianName,
    IReadOnlyList<ChargeServiceCapabilityDto> SupportedServices);
public sealed record GetProductsQuery(ChargeOperator Operator) : IRequest<IReadOnlyList<Providers.ChargeProductDto>>;
public sealed record GetOffersQuery(ChargeOperator Operator, string MobileNumber, Providers.ChargeOfferCategory Category)
    : IRequest<IReadOnlyList<Providers.ChargeProductDto>>;
public sealed record CheckEligibilityQuery(ChargeOperator Operator, string MobileNumber, long AmountMinor, string ProviderProductId, int ProductCategory)
    : IRequest<Providers.ChargeEligibilityDto>;
public sealed record GetPostpaidBalanceQuery(ChargeOperator Operator, string MobileNumber)
    : IRequest<Providers.ChargePostpaidBalanceDto>;
public sealed record GetPinCategoriesQuery(ChargeOperator? Operator = null) : IRequest<IReadOnlyList<Providers.PinChargeCategoryDto>>;
public sealed record GetPackageTypesQuery : IRequest<IReadOnlyList<Providers.PackageTypeDto>>;

public sealed record ReconcileChargeRequestCommand(Guid RequestId, bool ForceManualReviewReset = false) : IRequest;
