using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

namespace Refahi.Modules.Flights.Domain.Repositories;

public interface IFlightBookingRepository
{
    Task<FlightBooking?> GetAsync(FlightBookingId id, CancellationToken cancellationToken = default);

    Task<FlightBooking?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<FlightBooking?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<FlightBooking?> GetByProviderBookingIdAsync(
        string providerName,
        string providerBookingId,
        CancellationToken cancellationToken = default);

    Task AddAsync(FlightBooking booking, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
