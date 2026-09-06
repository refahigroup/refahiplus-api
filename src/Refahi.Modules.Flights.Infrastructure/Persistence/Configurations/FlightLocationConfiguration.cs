using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

public sealed class FlightSearchCityConfiguration : IEntityTypeConfiguration<FlightSearchCity>
{
    public void Configure(EntityTypeBuilder<FlightSearchCity> b)
    {
        b.ToTable("search_cities");
        b.HasKey(x => x.CityCode);
        b.Property(x => x.CityCode).HasColumnName("city_code").HasMaxLength(3);
        b.Property(x => x.CityNameFa).HasColumnName("city_name_fa").HasMaxLength(200).IsRequired();
        b.Property(x => x.CityNameEn).HasColumnName("city_name_en").HasMaxLength(200).IsRequired();
        b.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
        b.Property(x => x.CountryNameFa).HasColumnName("country_name_fa").HasMaxLength(200).IsRequired();
        b.Property(x => x.CountryNameEn).HasColumnName("country_name_en").HasMaxLength(200).IsRequired();
    }
}

public sealed class FlightSearchMembershipConfiguration : IEntityTypeConfiguration<FlightSearchMembership>
{
    public void Configure(EntityTypeBuilder<FlightSearchMembership> b)
    {
        b.ToTable("search_memberships");
        b.HasKey(x => new { x.IsDomestic, x.CityCode, x.AirportCode });
        b.Property(x => x.CityCode).HasColumnName("city_code").HasMaxLength(3);
        b.Property(x => x.AirportCode).HasColumnName("airport_code").HasMaxLength(3);
        b.Property(x => x.IsDomestic).HasColumnName("is_domestic");
        b.Property(x => x.CityRank).HasColumnName("city_rank");
        b.Property(x => x.AirportRank).HasColumnName("airport_rank");
        b.HasOne<FlightSearchCity>().WithMany().HasForeignKey(x => x.CityCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FlightAirport>().WithMany().HasForeignKey(x => x.AirportCode).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.IsDomestic, x.AirportCode }).IsUnique();
    }
}

