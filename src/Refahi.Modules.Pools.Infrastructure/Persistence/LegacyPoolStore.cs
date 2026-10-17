using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Infrastructure.Persistence;
public sealed class LegacyPoolStore(PoolsDbContext db) : ILegacyPoolStore
{
    public async Task<IReadOnlyList<LegacyPoolBooking>> ListAsync(CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(x => !x.IsMock, ct)) throw new PoolException("داده غیرآزمایشی قدیمی وجود دارد؛ پاک‌سازی متوقف شد");
        return await db.Bookings.AsNoTracking().Select(x => new LegacyPoolBooking(x.Id, x.UserId, x.OrderId)).ToArrayAsync(ct);
    }
    public Task<bool> IsLegacyAsync(Guid id, Guid user, CancellationToken ct) => db.Bookings.AnyAsync(x => x.Id == id && x.UserId == user && x.IsMock, ct);
    public async Task DeleteAsync(Guid id, CancellationToken ct) => await db.Bookings.Where(x => x.Id == id && x.IsMock).ExecuteDeleteAsync(ct);
    public async Task CompleteCleanupAsync(CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(ct)) throw new PoolException("پاک‌سازی کامل نشده است");
        await db.Catalog.ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS pools.mock_reservations", ct);
    }
}
