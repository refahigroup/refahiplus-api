using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;

namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IHotelRequestRepository
{
    Task<HotelRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HotelRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<HotelRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<HotelRequest?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(HotelRequest request, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
