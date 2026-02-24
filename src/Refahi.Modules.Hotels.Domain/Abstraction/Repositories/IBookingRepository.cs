using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetAsync(BookingId id, CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}