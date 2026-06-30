using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;

public sealed class HotelBookingSagaRepository : IHotelBookingSagaRepository
{
    private readonly HotelsDbContext _dbContext;

    public HotelBookingSagaRepository(HotelsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<HotelBookingSagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
        => _dbContext.HotelBookingSagas.FirstOrDefaultAsync(s => s.SagaId == sagaId, cancellationToken);

    public Task<HotelBookingSagaState?> GetByHotelRequestIdAsync(Guid hotelRequestId, CancellationToken cancellationToken = default)
        => _dbContext.HotelBookingSagas.FirstOrDefaultAsync(s => s.HotelRequestId == hotelRequestId, cancellationToken);

    public Task<HotelBookingSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _dbContext.HotelBookingSagas.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<HotelBookingSagaState>> GetStuckAsync(
        IReadOnlyCollection<HotelBookingSagaStatus> statuses,
        DateTime olderThanUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (statuses.Count == 0 || take <= 0)
            return [];

        return await _dbContext.HotelBookingSagas
            .Where(s => statuses.Contains(s.Status) && s.UpdatedAt <= olderThanUtc)
            .OrderBy(s => s.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(HotelBookingSagaState saga, CancellationToken cancellationToken = default)
        => await _dbContext.HotelBookingSagas.AddAsync(saga, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
