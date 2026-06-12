using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;

public sealed class FlightOfferSnapshotRepository : IFlightOfferSnapshotRepository
{
    private readonly FlightsDbContext _dbContext;

    public FlightOfferSnapshotRepository(FlightsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FlightOfferSnapshot?> GetByTokenAsync(
        string offerToken,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.FlightOfferSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                offer => offer.OfferToken == offerToken,
                cancellationToken);
    }

    public async Task AddAsync(
        FlightOfferSnapshot offerSnapshot,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.FlightOfferSnapshots.AddAsync(offerSnapshot, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
