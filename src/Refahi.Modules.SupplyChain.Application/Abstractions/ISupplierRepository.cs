using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Abstractions;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, bool includeChildren, CancellationToken ct);
    Task<(IReadOnlyList<Supplier> Items, int Total)> GetPagedAsync(
        SupplierStatus? status, SupplierType? type, int? provinceId, string? search,
        int page, int size, CancellationToken ct);
    Task<bool> ExistsByNationalIdAsync(string nationalId, Guid? excludeId, CancellationToken ct);
    Task AddAsync(Supplier supplier, CancellationToken ct);
    void Update(Supplier supplier);
    Task SaveChangesAsync(CancellationToken ct);
}
