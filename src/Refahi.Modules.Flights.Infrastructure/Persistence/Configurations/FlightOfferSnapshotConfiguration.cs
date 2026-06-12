using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

public sealed class FlightOfferSnapshotConfiguration : IEntityTypeConfiguration<FlightOfferSnapshot>
{
    public void Configure(EntityTypeBuilder<FlightOfferSnapshot> builder)
    {
        builder.ToTable("flight_search_offer_snapshots");

        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(offer => offer.OfferToken)
            .IsRequired()
            .HasMaxLength(120)
            .HasColumnName("offer_token");

        builder.Property(offer => offer.ProviderName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("provider_name");

        builder.Property(offer => offer.ProviderFareSourceCode)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("provider_fare_source_code");

        builder.Property(offer => offer.ProviderSearchId)
            .HasMaxLength(200)
            .HasColumnName("provider_search_id");

        builder.Property(offer => offer.ProviderTraceId)
            .HasMaxLength(200)
            .HasColumnName("provider_trace_id");

        builder.Property(offer => offer.TotalFareAmount)
            .IsRequired()
            .HasColumnName("total_fare_amount");

        builder.Property(offer => offer.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(offer => offer.PublicOfferSnapshotJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("public_offer_snapshot");

        builder.Property(offer => offer.ProviderSnapshotJson)
            .HasColumnType("jsonb")
            .HasColumnName("provider_snapshot");

        builder.Property(offer => offer.CreatedAtUtc)
            .IsRequired()
            .HasColumnName("created_at_utc");

        builder.Property(offer => offer.ExpiresAtUtc)
            .IsRequired()
            .HasColumnName("expires_at_utc");

        builder.HasIndex(offer => offer.OfferToken)
            .IsUnique()
            .HasDatabaseName("ux_flight_search_offer_snapshots_offer_token");

        builder.HasIndex(offer => offer.ExpiresAtUtc)
            .HasDatabaseName("ix_flight_search_offer_snapshots_expires_at_utc");
    }
}
