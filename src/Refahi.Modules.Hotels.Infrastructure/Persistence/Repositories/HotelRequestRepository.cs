using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;

public sealed class HotelRequestRepository : IHotelRequestRepository
{
    private readonly HotelsDbContext _dbContext;

    public HotelRequestRepository(HotelsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<HotelRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.HotelRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<HotelRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.HotelRequests.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

    public Task<HotelRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _dbContext.HotelRequests.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public Task<HotelRequest?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => _dbContext.HotelRequests.FirstOrDefaultAsync(
            r => r.UserId == userId && r.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task AddAsync(HotelRequest request, CancellationToken cancellationToken = default)
        => await _dbContext.HotelRequests.AddAsync(request, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
