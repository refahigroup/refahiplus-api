using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;

public sealed class FlightBookingRepository : IFlightBookingRepository
{
    private readonly FlightsDbContext _dbContext;

    public FlightBookingRepository(FlightsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FlightBooking?> GetAsync(
        FlightBookingId id,
        CancellationToken cancellationToken = default)
    {
        return await WithDetails()
            .FirstOrDefaultAsync(booking => booking.Id.Equals(id), cancellationToken);
    }

    public async Task<FlightBooking?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await WithDetails()
            .FirstOrDefaultAsync(booking => booking.OrderId == orderId, cancellationToken);
    }

    public async Task<FlightBooking?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await WithDetails()
            .FirstOrDefaultAsync(booking => booking.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<FlightBooking?> GetByProviderBookingIdAsync(
        string providerName,
        string providerBookingId,
        CancellationToken cancellationToken = default)
    {
        return await WithDetails()
            .FirstOrDefaultAsync(
                booking =>
                    booking.Provider.ProviderName == providerName
                    && booking.ProviderBooking != null
                    && booking.ProviderBooking.ProviderBookingId == providerBookingId,
                cancellationToken);
    }

    public async Task AddAsync(FlightBooking booking, CancellationToken cancellationToken = default)
    {
        await _dbContext.FlightBookings.AddAsync(booking, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<FlightBooking> WithDetails()
    {
        return _dbContext.FlightBookings
            .Include(booking => booking.Passengers)
            .Include(booking => booking.Segments)
            .Include(booking => booking.IssuedTickets)
            .Include(booking => booking.CancellationRequests);
    }
}
