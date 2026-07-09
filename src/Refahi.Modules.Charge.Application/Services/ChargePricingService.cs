using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Services;

public sealed class ChargePricingService
{
    private readonly IChargeMarkupRuleRepository _rules;
    public ChargePricingService(IChargeMarkupRuleRepository rules) => _rules = rules;

    public async Task<ChargePriceResult> CalculateAsync(
        ChargeOperator @operator, 
        ChargeServiceType serviceType,
        long providerCostMinor, 
        DateTime nowUtc, 
        CancellationToken ct
    )
    {
        var rule = await _rules.FindApplicableAsync(@operator, serviceType, nowUtc, ct);

        if (rule is null) 
            return new(null, 0, 0, 0, providerCostMinor);

        var percentAmount = checked((long)Math.Round(providerCostMinor * rule.Percent / 100m, 0, MidpointRounding.AwayFromZero));

        var markup = checked(percentAmount + rule.FixedAmountMinor);

        return new(
            rule.Id, 
            rule.Percent, 
            rule.FixedAmountMinor, 
            markup, 
            checked(providerCostMinor + markup)
        );
    }
}

public sealed record ChargePriceResult(Guid? RuleId, decimal Percent, long FixedAmountMinor, long MarkupAmountMinor, long FinalAmountMinor);
