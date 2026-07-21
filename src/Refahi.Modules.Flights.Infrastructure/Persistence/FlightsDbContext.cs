using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;
using Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Flights.Infrastructure.Persistence;

public sealed class FlightsDbContext : DbContext
{
    public FlightsDbContext(DbContextOptions<FlightsDbContext> options) : base(options)
    {
    }

    public DbSet<FlightBooking> FlightBookings => Set<FlightBooking>();

    public DbSet<FlightOfferSnapshot> FlightOfferSnapshots => Set<FlightOfferSnapshot>();

    public DbSet<FlightAirport> FlightAirports => Set<FlightAirport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("flights");

        modelBuilder.ApplyConfiguration(new FlightBookingConfiguration());
        modelBuilder.ApplyConfiguration(new FlightOfferSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new FlightAirportConfiguration());
    }
}
