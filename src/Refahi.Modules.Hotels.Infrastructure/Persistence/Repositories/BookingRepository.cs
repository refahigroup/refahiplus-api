using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories
{
    public sealed class BookingRepository : IBookingRepository
    {
        private readonly HotelsDbContext _dbContext;

        public BookingRepository(HotelsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Booking?> GetAsync(BookingId id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Bookings
                .Include(b => b.Guests) // چون Guests owned collection است، Include لازم است
                .FirstOrDefaultAsync(b => b.Id.Equals(id), cancellationToken);
        }

        public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            await _dbContext.Bookings.AddAsync(booking, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
