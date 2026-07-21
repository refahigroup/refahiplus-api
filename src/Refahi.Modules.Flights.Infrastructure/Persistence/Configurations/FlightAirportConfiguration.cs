using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

public sealed class FlightAirportConfiguration : IEntityTypeConfiguration<FlightAirport>
{
    public void Configure(EntityTypeBuilder<FlightAirport> builder)
    {
        builder.ToTable("airports");
        builder.HasKey(airport => airport.IataCode);

        builder.Property(airport => airport.IataCode).HasColumnName("iata_code").HasMaxLength(3).ValueGeneratedNever();
        builder.Property(airport => airport.IcaoCode).HasColumnName("icao_code").HasMaxLength(4);
        builder.Property(airport => airport.CityCode).HasColumnName("city_code").HasMaxLength(3).IsRequired();
        builder.Property(airport => airport.AirportNameFa).HasColumnName("airport_name_fa").HasMaxLength(300).IsRequired();
        builder.Property(airport => airport.AirportNameEn).HasColumnName("airport_name_en").HasMaxLength(300).IsRequired();
        builder.Property(airport => airport.CityNameFa).HasColumnName("city_name_fa").HasMaxLength(200).IsRequired();
        builder.Property(airport => airport.CityNameEn).HasColumnName("city_name_en").HasMaxLength(200).IsRequired();
        builder.Property(airport => airport.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
        builder.Property(airport => airport.CountryNameFa).HasColumnName("country_name_fa").HasMaxLength(200).IsRequired();
        builder.Property(airport => airport.CountryNameEn).HasColumnName("country_name_en").HasMaxLength(200).IsRequired();
        builder.Property(airport => airport.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
        builder.Property(airport => airport.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
        builder.Property(airport => airport.IsPopular).HasColumnName("is_popular").IsRequired();
        builder.Property(airport => airport.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(airport => airport.SourceVersion).HasColumnName("source_version").HasMaxLength(50).IsRequired();
        builder.Property(airport => airport.TranslationSource).HasColumnName("translation_source").HasMaxLength(50).IsRequired();
        builder.Property(airport => airport.SearchText).HasColumnName("search_text").HasColumnType("text").IsRequired();
        builder.Property(airport => airport.ImportedAtUtc).HasColumnName("imported_at_utc").IsRequired();

        builder.HasIndex(airport => airport.IcaoCode).HasDatabaseName("ix_flight_airports_icao_code");
        builder.HasIndex(airport => new { airport.IsActive, airport.IsPopular }).HasDatabaseName("ix_flight_airports_active_popular");
    }
}
