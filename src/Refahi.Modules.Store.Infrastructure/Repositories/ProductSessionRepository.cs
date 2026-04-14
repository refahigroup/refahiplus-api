using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ProductSessionRepository : IProductSessionRepository
{
    private readonly StoreDbContext _db;

    public ProductSessionRepository(StoreDbContext db) => _db = db;

    public Task<ProductSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ProductSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<ProductSession>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => _db.ProductSessions
            .Where(s => s.ProductId == productId)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(ct);

    public Task<List<ProductSession>> GetByProductIdAndDateAsync(
        Guid productId, DateOnly date, CancellationToken ct = default)
        => _db.ProductSessions
            .Where(s => s.ProductId == productId && s.Date == date)
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);

    public Task<List<ProductSession>> GetAvailableByProductIdAsync(
        Guid productId, CancellationToken ct = default)
        => _db.ProductSessions
            .Where(s => s.ProductId == productId && s.IsActive && !s.IsCancelled && s.SoldCount < s.Capacity)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(ct);

    public async Task UpdateAsync(ProductSession session, CancellationToken ct = default)
    {
        _db.ProductSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }
}
