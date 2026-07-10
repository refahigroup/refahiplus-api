using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;

public sealed class ChargeMarkupRuleRepository : IChargeMarkupRuleRepository
{
    private readonly ChargeDbContext _db;
    public ChargeMarkupRuleRepository(ChargeDbContext db)
    {
        _db = db;
    }

    public Task<ChargeMarkupRule?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.MarkupRules.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<ChargeMarkupRule>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.MarkupRules
                        .AsNoTracking()
                        .OrderByDescending(x => x.IsActive)
                        .ThenByDescending(x => x.EffectiveFrom)
                        .ToListAsync(ct);
    }

    public Task<ChargeMarkupRule?> FindApplicableAsync(ChargeOperator @operator, ChargeServiceType serviceType, DateTime nowUtc, CancellationToken ct = default)
    {
        return _db.MarkupRules.AsNoTracking()
                              .Where(x => x.IsActive && x.EffectiveFrom <= nowUtc && (x.EffectiveTo == null || x.EffectiveTo > nowUtc)
                                    && (x.Operator == null || x.Operator == @operator) && (x.ServiceType == null || x.ServiceType == serviceType))
                              .OrderByDescending(x => x.Operator == @operator && x.ServiceType == serviceType)
                              .ThenByDescending(x => x.ServiceType == serviceType && x.Operator == null)
                              .ThenByDescending(x => x.Operator == @operator && x.ServiceType == null)
                              .ThenByDescending(x => x.EffectiveFrom)
                              .FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasOverlapAsync(Guid? excludingId, ChargeOperator? @operator, ChargeServiceType? serviceType, DateTime from, DateTime? to, CancellationToken ct = default)
    {
        return _db.MarkupRules.AnyAsync(x => x.IsActive && (!excludingId.HasValue || x.Id != excludingId)
            && x.Operator == @operator && x.ServiceType == serviceType
            && (to == null || x.EffectiveFrom < to) && (x.EffectiveTo == null || x.EffectiveTo > from), ct);
    }

    public async Task AddAsync(ChargeMarkupRule rule, CancellationToken ct = default)
    {
        await _db.MarkupRules.AddAsync(rule, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }
}
