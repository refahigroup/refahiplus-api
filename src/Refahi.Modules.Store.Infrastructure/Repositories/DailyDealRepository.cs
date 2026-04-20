using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class DailyDealRepository : IDailyDealRepository
{
    private readonly StoreDbContext _db;

    public DailyDealRepository(StoreDbContext db) => _db = db;

    public Task<DailyDeal?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.DailyDeals.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<DailyDeal?> GetActiveByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.DailyDeals.FirstOrDefaultAsync(
            d => d.ProductId == productId && d.IsActive
              && d.StartTime <= now && d.EndTime >= now, ct);
    }

    public Task<List<DailyDeal>> GetCurrentlyActiveAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.DailyDeals
            .Where(d => d.IsActive && d.StartTime <= now && d.EndTime >= now)
            .ToListAsync(ct);
    }

    public Task<List<DailyDeal>> GetAllAsync(int? moduleId = null, CancellationToken ct = default)
    {
        var query = _db.DailyDeals.AsQueryable();
        if (moduleId.HasValue)
            query = query.Where(d => d.ModuleId == moduleId.Value);
        return query.OrderByDescending(d => d.StartTime).ToListAsync(ct);
    }

    public async Task AddAsync(DailyDeal deal, CancellationToken ct = default)
    {
        await _db.DailyDeals.AddAsync(deal, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DailyDeal deal, CancellationToken ct = default)
    {
        _db.DailyDeals.Update(deal);
        await _db.SaveChangesAsync(ct);
    }
}
