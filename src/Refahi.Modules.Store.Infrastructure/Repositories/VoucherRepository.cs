using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class VoucherRepository(StoreDbContext db) : IVoucherRepository
{
    public Task<Voucher?> GetByItemSequenceAsync(
        Guid itemId,
        int sequence,
        CancellationToken ct = default
    ) =>
        db.Vouchers.SingleOrDefaultAsync(
            x => x.StoreOrderItemId == itemId && x.SequenceNumber == sequence,
            ct
        );

    public Task<Voucher?> GetByCodeHashAsync(string hash, CancellationToken ct = default) =>
        db.Vouchers.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);

    public Task<Voucher?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Vouchers.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Voucher>> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        await db
            .Vouchers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IssuedAtUtc)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Voucher>> GetAllAsync(CancellationToken ct = default) =>
        await db.Vouchers.AsNoTracking().OrderByDescending(x => x.IssuedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<Voucher>> GetByStoreOrderAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await db
            .Vouchers.Where(x => x.StoreOrderId == id)
            .OrderBy(x => x.StoreOrderItemId)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(ct);

    public Task<VoucherRedemption?> GetRedemptionByIdempotencyAsync(
        Guid userId,
        string key,
        CancellationToken ct = default
    ) =>
        db
            .VoucherRedemptions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.VendorUserId == userId && x.IdempotencyKey == key, ct);

    public async Task<VoucherRedemptionHistoryPage> GetRedemptionHistoryAsync(
        Guid supplierId,
        Guid? shopId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query =
            from redemption in db.VoucherRedemptions.AsNoTracking()
            join voucher in db.Vouchers.AsNoTracking() on redemption.VoucherId equals voucher.Id
            where
                redemption.SupplierId == supplierId
                && voucher.SupplierId == supplierId
                && (!shopId.HasValue || redemption.ShopId == shopId.Value)
            select new VoucherRedemptionHistoryRow(
                voucher.Id,
                voucher.StoreOrderId,
                voucher.SequenceNumber,
                voucher.ProductId,
                voucher.ProductTitle,
                redemption.SupplierId,
                redemption.ShopId,
                voucher.RedeemedShopName ?? voucher.ShopName,
                redemption.VendorUserId,
                redemption.RedeemedAtUtc
            );

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.RedeemedAtUtc)
            .ThenBy(x => x.VoucherId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new(total, items);
    }

    public async Task AddAsync(Voucher voucher, CancellationToken ct = default)
    {
        db.Vouchers.Add(voucher);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUnique(ex))
        {
            db.Entry(voucher).State = EntityState.Detached;
            throw new StoreDomainException("ووچر تکراری شناسایی شد", "VOUCHER_UNIQUE_CONFLICT");
        }
    }

    public async Task RedeemAsync(
        Voucher voucher,
        VoucherRedemption redemption,
        CancellationToken ct = default
    )
    {
        db.VoucherRedemptions.Add(redemption);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new StoreConcurrencyException(ex);
        }
        catch (DbUpdateException ex) when (IsUnique(ex))
        {
            db.ChangeTracker.Clear();
            throw new StoreDomainException(
                "کلید یکتایی قبلاً استفاده شده است",
                "VOUCHER_IDEMPOTENCY_CONFLICT"
            );
        }
    }

    public async Task UpdateAsync(Voucher voucher, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new StoreConcurrencyException(ex);
        }
    }

    public async Task UpdateRangeAsync(
        IReadOnlyCollection<Voucher> vouchers,
        CancellationToken ct = default
    )
    {
        if (vouchers.Count == 0)
            return;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new StoreConcurrencyException(ex);
        }
    }

    private static bool IsUnique(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
