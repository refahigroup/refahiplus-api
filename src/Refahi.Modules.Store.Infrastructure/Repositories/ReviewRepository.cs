using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly StoreDbContext _db;

    public ReviewRepository(StoreDbContext db) => _db = db;

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Review>> GetByProductIdAsync(Guid productId, bool approvedOnly = true,
        CancellationToken ct = default)
        => _db.Reviews
            .Where(r => r.ProductId == productId && (!approvedOnly || r.IsApproved))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> UserHasReviewedAsync(Guid productId, Guid userId, CancellationToken ct = default)
        => _db.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId, ct);

    public async Task<(List<Review> Items, int Total)> GetPagedAsync(
        Guid productId, bool approvedOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Reviews.Where(r => r.ProductId == productId && (!approvedOnly || r.IsApproved));
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default)
    {
        var avg = await _db.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .AverageAsync(r => (double?)r.Rating, ct);
        return avg ?? 0.0;
    }

    public async Task AddAsync(Review review, CancellationToken ct = default)
    {
        await _db.Reviews.AddAsync(review, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        _db.Reviews.Update(review);
        await _db.SaveChangesAsync(ct);
    }
}
