using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class DeactivateMarkupRuleHandler : IRequestHandler<DeactivateMarkupRuleCommand>
{
    private readonly IChargeMarkupRuleRepository _rules;

    public DeactivateMarkupRuleHandler(IChargeMarkupRuleRepository rules)
    {
        _rules = rules;
    }

    public async Task<Unit> Handle(DeactivateMarkupRuleCommand c, CancellationToken ct)
    { 
        var rule = await _rules.GetAsync(c.RuleId, ct) ??
            throw new ArgumentException("قانون افزایش قیمت یافت نشد"); 
        
        rule.Deactivate(DateTime.UtcNow); 

        await _rules.SaveChangesAsync(ct); 
        
        return Unit.Value; 
    }
}
