using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

internal static class FlightGeographyMappingConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var airports = model.Entity<AirportGeographyMapping>();
        airports.ToTable("airport_geography_mappings");
        airports.HasKey(x => x.AirportIataCode);
        airports.Property(x => x.AirportIataCode).HasMaxLength(3);
        airports.Property(x => x.Evidence).HasColumnType("jsonb");
        airports.HasIndex(x => x.CityId);
        airports.HasOne<Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg.FlightAirport>()
            .WithMany().HasForeignKey(x => x.AirportIataCode).OnDelete(DeleteBehavior.Restrict);
        var cities = model.Entity<SnappTripFlightCityMapping>();
        cities.ToTable("snapptrip_city_mappings");
        cities.HasKey(x => x.ProviderCityCode);
        cities.Property(x => x.ProviderCityCode).HasMaxLength(3);
        cities.Property(x => x.Evidence).HasColumnType("jsonb");
        cities.HasIndex(x => x.CityId);
        cities.HasOne<FlightSearchCity>().WithMany().HasForeignKey(x => x.ProviderCityCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
