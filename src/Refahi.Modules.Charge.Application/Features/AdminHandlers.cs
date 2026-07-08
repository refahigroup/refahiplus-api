using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class UpsertMarkupRuleValidator : AbstractValidator<UpsertMarkupRuleCommand>
{
    public UpsertMarkupRuleValidator()
    {
        RuleFor(x => x.Percent).InclusiveBetween(0, 100).WithMessage("درصد افزایش قیمت باید بین صفر تا صد باشد");
        RuleFor(x => x.FixedAmountMinor).GreaterThanOrEqualTo(0).WithMessage("مبلغ ثابت نمی‌تواند منفی باشد");
        RuleFor(x => x.EffectiveTo).GreaterThan(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue).WithMessage("بازه اعتبار معتبر نیست");
    }
}

public sealed class UpsertMarkupRuleHandler : IRequestHandler<UpsertMarkupRuleCommand, MarkupRuleDto>
{
    private readonly IChargeMarkupRuleRepository _rules;
    public UpsertMarkupRuleHandler(IChargeMarkupRuleRepository rules) => _rules = rules;
    public async Task<MarkupRuleDto> Handle(UpsertMarkupRuleCommand c, CancellationToken ct)
    {
        if (await _rules.HasOverlapAsync(c.RuleId, c.Operator, c.ServiceType, c.EffectiveFrom, c.EffectiveTo, ct))
            throw new ArgumentException("برای این محدوده یک قانون فعال هم‌پوشان وجود دارد");
        ChargeMarkupRule rule;
        if (c.RuleId.HasValue)
        {
            rule = await _rules.GetAsync(c.RuleId.Value, ct) ?? throw new ArgumentException("قانون افزایش قیمت یافت نشد");
            rule.Update(c.Operator, c.ServiceType, c.Percent, c.FixedAmountMinor, c.EffectiveFrom, c.EffectiveTo, DateTime.UtcNow);
        }
        else
        {
            rule = ChargeMarkupRule.Create(c.Operator, c.ServiceType, c.Percent, c.FixedAmountMinor, c.EffectiveFrom, c.EffectiveTo, DateTime.UtcNow);
            await _rules.AddAsync(rule, ct);
        }
        await _rules.SaveChangesAsync(ct); return Map(rule);
    }
    internal static MarkupRuleDto Map(ChargeMarkupRule x) => new(x.Id, x.Operator, x.ServiceType, x.Percent, x.FixedAmountMinor, x.EffectiveFrom, x.EffectiveTo, x.IsActive);
}

public sealed class DeactivateMarkupRuleHandler : IRequestHandler<DeactivateMarkupRuleCommand>
{
    private readonly IChargeMarkupRuleRepository _rules; public DeactivateMarkupRuleHandler(IChargeMarkupRuleRepository rules) => _rules = rules;
    public async Task<Unit> Handle(DeactivateMarkupRuleCommand c, CancellationToken ct)
    { var rule = await _rules.GetAsync(c.RuleId, ct) ?? throw new ArgumentException("قانون افزایش قیمت یافت نشد"); rule.Deactivate(DateTime.UtcNow); await _rules.SaveChangesAsync(ct); return Unit.Value; }
}
public sealed class GetMarkupRulesHandler : IRequestHandler<GetMarkupRulesQuery, IReadOnlyList<MarkupRuleDto>>
{
    private readonly IChargeMarkupRuleRepository _rules; public GetMarkupRulesHandler(IChargeMarkupRuleRepository rules) => _rules = rules;
    public async Task<IReadOnlyList<MarkupRuleDto>> Handle(GetMarkupRulesQuery q, CancellationToken ct) => (await _rules.GetAllAsync(ct)).Select(UpsertMarkupRuleHandler.Map).ToList();
}

public sealed class ProviderAdminHandlers :
    IRequestHandler<GetProviderBalanceQuery, ProviderBalanceDto>,
    IRequestHandler<GetProviderErrorsQuery, IReadOnlyList<ProviderErrorDto>>,
    IRequestHandler<GetProviderChannelsQuery, IReadOnlyList<ProviderChannelDto>>,
    IRequestHandler<GetProviderTransactionReportQuery, ProviderReportDto>,
    IRequestHandler<GetProviderWalletChargeReportQuery, ProviderReportDto>
{
    private readonly IChargeProviderResolver _providers; public ProviderAdminHandlers(IChargeProviderResolver providers) => _providers = providers;
    public Task<ProviderBalanceDto> Handle(GetProviderBalanceQuery q, CancellationToken ct) => _providers.GetDefault().GetBalanceAsync(ct);
    public Task<IReadOnlyList<ProviderErrorDto>> Handle(GetProviderErrorsQuery q, CancellationToken ct) => _providers.GetDefault().GetErrorsAsync(ct);
    public Task<IReadOnlyList<ProviderChannelDto>> Handle(GetProviderChannelsQuery q, CancellationToken ct) => _providers.GetDefault().GetChannelsAsync(ct);
    public Task<ProviderReportDto> Handle(GetProviderTransactionReportQuery q, CancellationToken ct) => _providers.GetDefault().GetTransactionReportAsync(new(q.PageNumber, q.FromDate, q.ToDate), ct);
    public Task<ProviderReportDto> Handle(GetProviderWalletChargeReportQuery q, CancellationToken ct) => _providers.GetDefault().GetWalletChargeReportAsync(new(q.PageNumber, q.FromDate, q.ToDate), ct);
}
