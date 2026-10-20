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

    public async Task<HotelRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.HotelRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is not null && _dbContext.Entry(request).State == EntityState.Unchanged)
            await _dbContext.Entry(request).ReloadAsync(cancellationToken);
        return request;
    }

    public async Task<HotelRequest?> GetForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var request = await _dbContext.HotelRequests.FirstOrDefaultAsync(
            r => r.Id == id && r.UserId == userId,
            cancellationToken
        );
        if (request is not null && _dbContext.Entry(request).State == EntityState.Unchanged)
            await _dbContext.Entry(request).ReloadAsync(cancellationToken);
        return request;
    }

    public async Task<HotelRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.HotelRequests.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
        if (request is not null && _dbContext.Entry(request).State == EntityState.Unchanged)
            await _dbContext.Entry(request).ReloadAsync(cancellationToken);
        return request;
    }

    public Task<HotelRequest?> GetByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default
    ) =>
        _dbContext.HotelRequests.FirstOrDefaultAsync(
            r => r.UserId == userId && r.IdempotencyKey == idempotencyKey,
            cancellationToken
        );

    public async Task AddAsync(
        HotelRequest request,
        CancellationToken cancellationToken = default
    ) => await _dbContext.HotelRequests.AddAsync(request, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
