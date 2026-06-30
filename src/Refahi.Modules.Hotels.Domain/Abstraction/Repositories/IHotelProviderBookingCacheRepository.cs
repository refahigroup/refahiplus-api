using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;

namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IHotelProviderBookingCacheRepository
{
    Task<HotelProviderBookingCacheEntry?> GetAsync(
        string providerName,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<HotelProviderBookingCacheEntry?> GetByProviderBookingCodeAsync(
        string providerName,
        string providerBookingCode,
        CancellationToken cancellationToken = default);

    Task<HotelProviderBookingCacheEntry?> GetBySagaIdAsync(
        Guid sagaId,
        CancellationToken cancellationToken = default);

    Task AddAsync(HotelProviderBookingCacheEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
