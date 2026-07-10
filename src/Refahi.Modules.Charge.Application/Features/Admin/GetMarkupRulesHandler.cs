using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class GetMarkupRulesHandler : IRequestHandler<GetMarkupRulesQuery, IReadOnlyList<MarkupRuleDto>>
{
    private readonly IChargeMarkupRuleRepository _rules;

    public GetMarkupRulesHandler(IChargeMarkupRuleRepository rules)
    {
        _rules = rules;
    }

    public async Task<IReadOnlyList<MarkupRuleDto>> Handle(GetMarkupRulesQuery q, CancellationToken ct)
    {
        var all = await _rules.GetAllAsync(ct);

        return all.Select(UpsertMarkupRuleHandler.Map)
                  .ToList();
    }
}
