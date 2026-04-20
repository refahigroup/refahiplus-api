using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly StoreDbContext _db;

    public CartRepository(StoreDbContext db) => _db = db;

    public Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ModuleId == moduleId, ct);

    public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Cart cart, CancellationToken ct = default)
    {
        await _db.Carts.AddAsync(cart, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Cart cart, CancellationToken ct = default)
    {
        _db.Carts.Update(cart);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Cart cart, CancellationToken ct = default)
    {
        _db.Carts.Remove(cart);
        await _db.SaveChangesAsync(ct);
    }
}
