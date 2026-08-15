using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Repositories;

public sealed record VoucherSourceCounts(int Available, int Reserved, int Assigned, int Expired, int Disabled);

public sealed record VoucherSourceCodePage(int Total, IReadOnlyList<VoucherSourceCode> Items);

public interface IVoucherSourceRepository
{
    Task<VoucherSource?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VoucherSource>> GetBySupplierAsync(Guid supplierId, bool includeInactive, CancellationToken ct = default);
    Task<VoucherSourceCounts> GetCountsAsync(Guid sourceId, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<bool> IsUsedByActiveCatalogAsync(Guid sourceId, CancellationToken ct = default);
    Task<HashSet<string>> GetExistingHashesAsync(Guid supplierId, IReadOnlyCollection<string> hashes, CancellationToken ct = default);
    Task<VoucherCodeImportBatch?> GetImportBatchAsync(Guid sourceId, string idempotencyKey, CancellationToken ct = default);
    Task AddAsync(VoucherSource source, CancellationToken ct = default);
    Task UpdateAsync(VoucherSource source, CancellationToken ct = default);
    Task AddCodesAsync(VoucherCodeImportBatch batch, IReadOnlyCollection<VoucherSourceCode> codes, CancellationToken ct = default);
    Task<VoucherSourceCodePage> GetCodesAsync(Guid sourceId, VoucherSourceCodeStatus? status, int page, int pageSize, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<VoucherSourceCode?> GetCodeAsync(Guid sourceId, Guid codeId, CancellationToken ct = default);
    Task UpdateCodeAsync(VoucherSourceCode code, CancellationToken ct = default);
    Task<int> GetAvailableCountAsync(Guid sourceId, DateTimeOffset requiredUntilUtc, CancellationToken ct = default);
}
