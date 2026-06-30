using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IHotelBookingSagaRepository
{
    Task<HotelBookingSagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default);
    Task<HotelBookingSagaState?> GetByHotelRequestIdAsync(Guid hotelRequestId, CancellationToken cancellationToken = default);
    Task<HotelBookingSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotelBookingSagaState>> GetStuckAsync(
        IReadOnlyCollection<HotelBookingSagaStatus> statuses,
        DateTime olderThanUtc,
        int take,
        CancellationToken cancellationToken = default);
    Task AddAsync(HotelBookingSagaState saga, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
