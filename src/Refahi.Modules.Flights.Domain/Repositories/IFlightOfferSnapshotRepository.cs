using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;

namespace Refahi.Modules.Flights.Domain.Repositories;

public interface IFlightOfferSnapshotRepository
{
    Task<FlightOfferSnapshot?> GetByTokenAsync(string offerToken, CancellationToken cancellationToken = default);

    Task AddAsync(FlightOfferSnapshot offerSnapshot, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
