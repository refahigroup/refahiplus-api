using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence;

public sealed class HotelsDbContext : DbContext
{
    public HotelsDbContext(DbContextOptions<HotelsDbContext> options): base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("hotels");

        modelBuilder.ApplyConfiguration(new BookingEntityTypeConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
