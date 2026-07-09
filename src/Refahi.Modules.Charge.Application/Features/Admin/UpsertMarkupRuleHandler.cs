using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Repositories;
using System.Diagnostics;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class UpsertMarkupRuleValidator : AbstractValidator<UpsertMarkupRuleCommand>
{
    public UpsertMarkupRuleValidator()
    {
        RuleFor(x => x.Percent)
            .InclusiveBetween(0, 100)
            .WithMessage("درصد افزایش قیمت باید بین صفر تا صد باشد");

        RuleFor(x => x.FixedAmountMinor)
            .GreaterThanOrEqualTo(0)
            .WithMessage("مبلغ ثابت نمی‌تواند منفی باشد");

        RuleFor(x => x.EffectiveTo)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo.HasValue)
            .WithMessage("بازه اعتبار معتبر نیست");
    }
}

public sealed class UpsertMarkupRuleHandler : IRequestHandler<UpsertMarkupRuleCommand, MarkupRuleDto>
{
    private readonly IChargeMarkupRuleRepository _rules;
    public UpsertMarkupRuleHandler(IChargeMarkupRuleRepository rules) => _rules = rules;
    public async Task<MarkupRuleDto> Handle(UpsertMarkupRuleCommand c, CancellationToken ct)
    {
        bool hasOver = await _rules.HasOverlapAsync(
            c.RuleId, 
            c.Operator, 
            c.ServiceType, 
            c.EffectiveFrom, 
            c.EffectiveTo, 
            ct
        );

        if (hasOver)
            throw new ArgumentException("برای این محدوده یک قانون فعال هم‌پوشان وجود دارد");

        ChargeMarkupRule rule;

        if (c.RuleId.HasValue)
        {
            rule = await _rules.GetAsync(c.RuleId.Value, ct) ?? 
                throw new ArgumentException("قانون افزایش قیمت یافت نشد");

            rule.Update(
                c.Operator, 
                c.ServiceType, 
                c.Percent, 
                c.FixedAmountMinor, 
                c.EffectiveFrom, 
                c.EffectiveTo, 
                DateTime.UtcNow
            );
        }
        else
        {
            rule = ChargeMarkupRule.Create(
                c.Operator, 
                c.ServiceType, 
                c.Percent, 
                c.FixedAmountMinor, 
                c.EffectiveFrom, 
                c.EffectiveTo, 
                DateTime.UtcNow
            );

            await _rules.AddAsync(rule, ct);
        }

        await _rules.SaveChangesAsync(ct); 
        
        return Map(rule);
    }

    internal static MarkupRuleDto Map(ChargeMarkupRule x) => 
        new(
            x.Id, 
            x.Operator, 
            x.ServiceType, 
            x.Percent, 
            x.FixedAmountMinor, 
            x.EffectiveFrom, 
            x.EffectiveTo, 
            x.IsActive
        );
}
