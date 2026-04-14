using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Review>> GetByProductIdAsync(Guid productId, bool approvedOnly = true, CancellationToken ct = default);
    Task<bool> UserHasReviewedAsync(Guid productId, Guid userId, CancellationToken ct = default);
    Task<(List<Review> Items, int Total)> GetPagedAsync(
        Guid productId, bool approvedOnly, int page, int pageSize, CancellationToken ct = default);
    Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default);
    Task AddAsync(Review review, CancellationToken ct = default);
    Task UpdateAsync(Review review, CancellationToken ct = default);
}
