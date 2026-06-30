using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

public sealed class HotelBookingSagaStateEntityTypeConfiguration : IEntityTypeConfiguration<HotelBookingSagaState>
{
    public void Configure(EntityTypeBuilder<HotelBookingSagaState> builder)
    {
        builder.ToTable("hotel_booking_sagas");

        builder.HasKey(s => s.SagaId);

        builder.Property(s => s.SagaId).HasColumnName("saga_id");
        builder.Property(s => s.UserId).IsRequired().HasColumnName("user_id");
        builder.Property(s => s.HotelRequestId).IsRequired().HasColumnName("hotel_request_id");
        builder.Property(s => s.OrderId).HasColumnName("order_id");

        builder.Property(s => s.PaymentStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(HotelBookingPaymentStatus.None)
            .HasColumnName("payment_status");

        builder.Property(s => s.ProviderBookingStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(HotelProviderBookingStatus.None)
            .HasColumnName("provider_booking_status");

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(HotelBookingSagaStatus.Started)
            .HasColumnName("status");

        builder.Property(s => s.FailureReason)
            .HasMaxLength(1000)
            .HasColumnName("failure_reason");

        builder.Property(s => s.ProviderCancellationIdempotencyKey)
            .HasMaxLength(160)
            .HasColumnName("provider_cancellation_idempotency_key");

        builder.Property(s => s.ProviderCancellationReason)
            .HasMaxLength(1000)
            .HasColumnName("provider_cancellation_reason");

        builder.Property(s => s.ProviderCancellationRequestedAt)
            .HasColumnName("provider_cancellation_requested_at");

        builder.Property(s => s.ProviderCancellationCompletedAt)
            .HasColumnName("provider_cancellation_completed_at");

        builder.Property(s => s.ExternalUnresolvedAt)
            .HasColumnName("external_unresolved_at");

        builder.Property(s => s.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).IsRequired().HasColumnName("updated_at");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(s => s.HotelRequestId)
            .IsUnique()
            .HasDatabaseName("ix_hotel_booking_sagas_hotel_request_id");

        builder.HasIndex(s => s.OrderId)
            .IsUnique()
            .HasFilter("\"order_id\" IS NOT NULL")
            .HasDatabaseName("ix_hotel_booking_sagas_order_id");

        builder.HasIndex(s => new { s.Status, s.UpdatedAt })
            .HasDatabaseName("ix_hotel_booking_sagas_status_updated_at");

        builder.HasIndex(s => new { s.ProviderBookingStatus, s.UpdatedAt })
            .HasDatabaseName("ix_hotel_booking_sagas_provider_status_updated_at");
    }
}
