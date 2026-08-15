using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class VoucherSourceRepository(StoreDbContext db) : IVoucherSourceRepository
{
    public Task<VoucherSource?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.VoucherSources.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<VoucherSource>> GetBySupplierAsync(
        Guid supplierId, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.VoucherSources.AsNoTracking().Where(x => x.SupplierId == supplierId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return await query.OrderBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<VoucherSourceCounts> GetCountsAsync(
        Guid sourceId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        var query = db.VoucherSourceCodes.AsNoTracking().Where(x => x.VoucherSourceId == sourceId);
        return new VoucherSourceCounts(
            await query.CountAsync(x => x.Status == VoucherSourceCodeStatus.Available
                && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > nowUtc), ct),
            await query.CountAsync(x => x.Status == VoucherSourceCodeStatus.Reserved, ct),
            await query.CountAsync(x => x.Status == VoucherSourceCodeStatus.Assigned, ct),
            await query.CountAsync(x => x.Status == VoucherSourceCodeStatus.Available
                && x.ExpiresAtUtc.HasValue && x.ExpiresAtUtc <= nowUtc, ct),
            await query.CountAsync(x => x.Status == VoucherSourceCodeStatus.Disabled, ct));
    }

    public async Task<bool> IsUsedByActiveCatalogAsync(Guid sourceId, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(
                x => !x.IsDeleted && x.IsAvailable && x.VoucherSourceId == sourceId, ct))
            return true;
        return await db.ProductVariants.AnyAsync(
            x => x.IsAvailable && x.VoucherSourceId == sourceId, ct);
    }

    public async Task<HashSet<string>> GetExistingHashesAsync(
        Guid supplierId, IReadOnlyCollection<string> hashes, CancellationToken ct = default) =>
        (await db.VoucherSourceCodes.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && hashes.Contains(x.CodeHash))
            .Select(x => x.CodeHash).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

    public Task<VoucherCodeImportBatch?> GetImportBatchAsync(
        Guid sourceId, string idempotencyKey, CancellationToken ct = default) =>
        db.VoucherCodeImportBatches.AsNoTracking().SingleOrDefaultAsync(
            x => x.VoucherSourceId == sourceId && x.IdempotencyKey == idempotencyKey, ct);

    public async Task AddAsync(VoucherSource source, CancellationToken ct = default)
    {
        db.VoucherSources.Add(source);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(VoucherSource source, CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new StoreConcurrencyException(ex); }
    }

    public async Task AddCodesAsync(
        VoucherCodeImportBatch batch,
        IReadOnlyCollection<VoucherSourceCode> codes,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.VoucherCodeImportBatches.Add(batch);
        db.VoucherSourceCodes.AddRange(codes);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw new StoreDomainException("کد یا کلید ورود تکراری است", "VOUCHER_CODE_IMPORT_CONFLICT");
        }
    }

    public async Task<VoucherSourceCodePage> GetCodesAsync(
        Guid sourceId, VoucherSourceCodeStatus? status, int page, int pageSize,
        DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        var query = db.VoucherSourceCodes.AsNoTracking().Where(x => x.VoucherSourceId == sourceId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.RegisteredAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new VoucherSourceCodePage(total, items);
    }

    public Task<VoucherSourceCode?> GetCodeAsync(Guid sourceId, Guid codeId, CancellationToken ct = default) =>
        db.VoucherSourceCodes.SingleOrDefaultAsync(x => x.Id == codeId && x.VoucherSourceId == sourceId, ct);

    public async Task UpdateCodeAsync(VoucherSourceCode code, CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new StoreConcurrencyException(ex); }
    }

    public Task<int> GetAvailableCountAsync(
        Guid sourceId, DateTimeOffset requiredUntilUtc, CancellationToken ct = default) =>
        db.VoucherSourceCodes.CountAsync(x => x.VoucherSourceId == sourceId
            && x.Status == VoucherSourceCodeStatus.Available
            && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > requiredUntilUtc), ct);
}
