using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Domain.Repositories;

public interface IChargeMarkupRuleRepository
{
    Task<ChargeMarkupRule?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeMarkupRule>> GetAllAsync(CancellationToken ct = default);
    Task<ChargeMarkupRule?> FindApplicableAsync(ChargeOperator @operator, ChargeServiceType serviceType, DateTime nowUtc, CancellationToken ct = default);
    Task<bool> HasOverlapAsync(Guid? excludingId, ChargeOperator? @operator, ChargeServiceType? serviceType, DateTime from, DateTime? to, CancellationToken ct = default);
    Task AddAsync(ChargeMarkupRule rule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
