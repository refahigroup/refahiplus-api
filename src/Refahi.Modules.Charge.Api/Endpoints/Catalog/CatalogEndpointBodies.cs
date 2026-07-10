using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed record OffersBody(string MobileNumber, ChargeOfferCategory Category = ChargeOfferCategory.All);

public sealed record MobileBody(string MobileNumber);

public sealed record EligibilityBody(
    ChargeOperator Operator,
    string MobileNumber,
    long AmountMinor,
    string ProviderProductId,
    int ProductCategory);

public sealed record ChargeQuoteBody(
    ChargeOperator Operator,
    ChargeServiceType ServiceType,
    string DestinationMobileNumber,
    string? ProviderProductId,
    long? RequestedAmountMinor,
    int? PinCategoryId,
    int PinCount = 1);
