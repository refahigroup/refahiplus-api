using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed record CreateChargeRequestBody(
    ChargeOperator Operator,
    ChargeServiceType ServiceType,
    string DestinationMobileNumber,
    string? OriginMobileNumber,
    string? ProviderProductId,
    long? RequestedAmountMinor,
    int? PinCategoryId,
    long ExpectedFinalAmountMinor,
    int PinCount = 1);
