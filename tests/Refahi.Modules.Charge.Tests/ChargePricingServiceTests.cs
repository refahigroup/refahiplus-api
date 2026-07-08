using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargePricingServiceTests
{
    [Fact]
    public async Task Percentage_and_fixed_markup_are_applied_once()
    {
        var now = DateTime.UtcNow; var rule = ChargeMarkupRule.Create(ChargeOperator.Irancell, ChargeServiceType.PinCharge, 7.5m, 1_001, now.AddMinutes(-1), null, now);
        var service = new ChargePricingService(new RuleRepository(rule));
        var result = await service.CalculateAsync(ChargeOperator.Irancell, ChargeServiceType.PinCharge, 100_001, now, default);
        Assert.Equal(8_501, result.MarkupAmountMinor);
        Assert.Equal(108_502, result.FinalAmountMinor);
    }

    [Fact]
    public async Task Missing_rule_means_zero_markup()
    {
        var result = await new ChargePricingService(new RuleRepository(null)).CalculateAsync(ChargeOperator.Mci, ChargeServiceType.DirectCharge, 50_000, DateTime.UtcNow, default);
        Assert.Equal(0, result.MarkupAmountMinor); Assert.Equal(50_000, result.FinalAmountMinor);
    }

    private sealed class RuleRepository(ChargeMarkupRule? rule) : IChargeMarkupRuleRepository
    {
        public Task<ChargeMarkupRule?> FindApplicableAsync(ChargeOperator o, ChargeServiceType s, DateTime n, CancellationToken c = default) => Task.FromResult(rule);
        public Task<ChargeMarkupRule?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(rule);
        public Task<IReadOnlyList<ChargeMarkupRule>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeMarkupRule>>(rule is null ? [] : [rule]);
        public Task<bool> HasOverlapAsync(Guid? id, ChargeOperator? o, ChargeServiceType? s, DateTime f, DateTime? t, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(ChargeMarkupRule r, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
