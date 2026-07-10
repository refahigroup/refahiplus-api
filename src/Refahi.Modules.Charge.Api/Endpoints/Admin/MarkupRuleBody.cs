using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed record MarkupRuleBody(
    ChargeOperator? Operator,
    ChargeServiceType? ServiceType,
    decimal Percent,
    long FixedAmountMinor,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);
