using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;
using Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence;

public sealed class HotelsDbContext : DbContext
{
    public HotelsDbContext(DbContextOptions<HotelsDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<HotelRequest> HotelRequests => Set<HotelRequest>();
    public DbSet<HotelBookingSagaState> HotelBookingSagas => Set<HotelBookingSagaState>();
    public DbSet<HotelProviderBookingCacheEntry> HotelProviderBookingCacheEntries => Set<HotelProviderBookingCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("hotels");

        modelBuilder.ApplyConfiguration(new BookingEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new HotelRequestEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new HotelBookingSagaStateEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new HotelProviderBookingCacheEntryEntityTypeConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
