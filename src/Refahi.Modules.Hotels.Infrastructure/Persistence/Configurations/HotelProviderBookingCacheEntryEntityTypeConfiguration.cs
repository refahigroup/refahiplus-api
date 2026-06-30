using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

public sealed class HotelProviderBookingCacheEntryEntityTypeConfiguration
    : IEntityTypeConfiguration<HotelProviderBookingCacheEntry>
{
    public void Configure(EntityTypeBuilder<HotelProviderBookingCacheEntry> builder)
    {
        builder.ToTable("hotel_provider_booking_cache");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProviderName).IsRequired().HasMaxLength(80).HasColumnName("provider_name");
        builder.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(120).HasColumnName("idempotency_key");
        builder.Property(e => e.RequestHash).IsRequired().HasMaxLength(128).HasColumnName("request_hash");
        builder.Property(e => e.SagaId).IsRequired().HasColumnName("saga_id");
        builder.Property(e => e.HotelRequestId).IsRequired().HasColumnName("hotel_request_id");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(ProviderBookingCacheStatus.InProgress)
            .HasColumnName("status");

        builder.Property(e => e.ProviderBookingCode)
            .HasMaxLength(120)
            .HasColumnName("provider_booking_code");

        builder.Property(e => e.ResponseJson)
            .HasColumnType("jsonb")
            .HasColumnName("response_json");

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000)
            .HasColumnName("failure_reason");

        builder.Property(e => e.CancellationIdempotencyKey)
            .HasMaxLength(160)
            .HasColumnName("cancellation_idempotency_key");

        builder.Property(e => e.CancellationReason)
            .HasMaxLength(1000)
            .HasColumnName("cancellation_reason");

        builder.Property(e => e.AttemptCount).IsRequired().HasColumnName("attempt_count");
        builder.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).IsRequired().HasColumnName("updated_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(e => e.CancellationRequestedAt).HasColumnName("cancellation_requested_at");
        builder.Property(e => e.CancellationCompletedAt).HasColumnName("cancellation_completed_at");
        builder.Property(e => e.ExternalUnresolvedAt).HasColumnName("external_unresolved_at");

        builder.HasIndex(e => new { e.ProviderName, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_hotel_provider_booking_cache_provider_idem");

        builder.HasIndex(e => new { e.ProviderName, e.ProviderBookingCode })
            .IsUnique()
            .HasFilter("\"provider_booking_code\" IS NOT NULL")
            .HasDatabaseName("ux_hotel_provider_booking_cache_provider_code");

        builder.HasIndex(e => new { e.Status, e.UpdatedAt })
            .HasDatabaseName("ix_hotel_provider_booking_cache_status_updated_at");

        builder.HasIndex(e => e.CancellationIdempotencyKey)
            .HasFilter("\"cancellation_idempotency_key\" IS NOT NULL")
            .HasDatabaseName("ix_hotel_provider_booking_cache_cancel_idem");
    }
}
