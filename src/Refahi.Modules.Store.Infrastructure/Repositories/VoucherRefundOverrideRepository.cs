using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class VoucherRefundOverrideRepository(StoreDbContext db)
    : IVoucherRefundOverrideRepository
{
    public Task<VoucherRefundOverride?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.VoucherRefundOverrides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<VoucherRefundOverride?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken ct = default
    ) =>
        db
            .VoucherRefundOverrides.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == orderId, ct);

    public Task<VoucherRefundOverride?> GetByIdempotencyKeyAsync(
        string key,
        CancellationToken ct = default
    ) =>
        db
            .VoucherRefundOverrides.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);

    public async Task<IReadOnlyList<VoucherRefundOverrideAttempt>> GetAttemptsAsync(
        Guid overrideId,
        CancellationToken ct = default
    ) =>
        await db
            .VoucherRefundOverrideAttempts.AsNoTracking()
            .Where(x => x.VoucherRefundOverrideId == overrideId)
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(ct);

    public async Task AddAsync(VoucherRefundOverride value, CancellationToken ct = default)
    {
        db.VoucherRefundOverrides.Add(value);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUnique(ex))
        {
            db.Entry(value).State = EntityState.Detached;
            throw new StoreDomainException(
                "مجوز استثنای بازگشت وجه قبلاً ثبت شده است",
                "VOUCHER_REFUND_OVERRIDE_CONFLICT"
            );
        }
    }

    public async Task AddAttemptAsync(
        VoucherRefundOverrideAttempt value,
        CancellationToken ct = default
    )
    {
        db.VoucherRefundOverrideAttempts.Add(value);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUnique(ex))
        {
            db.Entry(value).State = EntityState.Detached;
            throw new StoreDomainException(
                "نتیجه تلاش بازگشت وجه قبلاً ثبت شده است",
                "VOUCHER_REFUND_ATTEMPT_CONFLICT"
            );
        }
    }

    private static bool IsUnique(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
