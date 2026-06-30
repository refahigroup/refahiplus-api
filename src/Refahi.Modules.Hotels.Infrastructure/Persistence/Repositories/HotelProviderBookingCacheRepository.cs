using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;

public sealed class HotelProviderBookingCacheRepository : IHotelProviderBookingCacheRepository
{
    private readonly HotelsDbContext _dbContext;

    public HotelProviderBookingCacheRepository(HotelsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<HotelProviderBookingCacheEntry?> GetAsync(
        string providerName,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => _dbContext.HotelProviderBookingCacheEntries.FirstOrDefaultAsync(
            e => e.ProviderName == providerName && e.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<HotelProviderBookingCacheEntry?> GetByProviderBookingCodeAsync(
        string providerName,
        string providerBookingCode,
        CancellationToken cancellationToken = default)
        => _dbContext.HotelProviderBookingCacheEntries.FirstOrDefaultAsync(
            e => e.ProviderName == providerName && e.ProviderBookingCode == providerBookingCode,
            cancellationToken);

    public Task<HotelProviderBookingCacheEntry?> GetBySagaIdAsync(
        Guid sagaId,
        CancellationToken cancellationToken = default)
        => _dbContext.HotelProviderBookingCacheEntries.FirstOrDefaultAsync(
            e => e.SagaId == sagaId,
            cancellationToken);

    public async Task AddAsync(HotelProviderBookingCacheEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.HotelProviderBookingCacheEntries.AddAsync(entry, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
