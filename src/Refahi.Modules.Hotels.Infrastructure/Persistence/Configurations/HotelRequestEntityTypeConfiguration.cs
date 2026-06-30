using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations;

public sealed class HotelRequestEntityTypeConfiguration : IEntityTypeConfiguration<HotelRequest>
{
    public void Configure(EntityTypeBuilder<HotelRequest> builder)
    {
        builder.ToTable("hotel_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.UserId).IsRequired().HasColumnName("user_id");
        builder.Property(r => r.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).IsRequired().HasColumnName("updated_at");

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(HotelRequestStatus.Created)
            .HasColumnName("status");

        builder.Property(r => r.ExpireAt).IsRequired().HasColumnName("expire_at");

        builder.Property(r => r.ProviderName)
            .IsRequired()
            .HasMaxLength(80)
            .HasColumnName("provider_name");

        builder.Property(r => r.ProviderHotelId).IsRequired().HasColumnName("provider_hotel_id");
        builder.Property(r => r.ProviderRoomId).IsRequired().HasColumnName("provider_room_id");
        builder.Property(r => r.ProviderBookingCode).HasMaxLength(100).HasColumnName("provider_booking_code");
        builder.Property(r => r.ProviderConfirmedAt).HasColumnName("provider_confirmed_at");

        builder.Property(r => r.SearchCriteriaSnapshot)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("search_criteria_snapshot");

        builder.Property(r => r.SelectedHotelSnapshot)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("selected_hotel_snapshot");

        builder.Property(r => r.SelectedRoomSnapshot)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("selected_room_snapshot");

        builder.Property(r => r.TotalPrice).IsRequired().HasColumnName("total_price");

        builder.Property(r => r.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("IRR")
            .HasColumnName("currency");

        builder.Property(r => r.Breakdown)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("breakdown");

        builder.Property(r => r.Fees)
            .HasColumnType("jsonb")
            .HasColumnName("fees");

        builder.Property(r => r.GuestInfoSnapshot)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("guest_info_snapshot");

        builder.Property(r => r.OrderId).HasColumnName("order_id");

        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("idempotency_key");

        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_hotel_requests_user_id");

        builder.HasIndex(r => new { r.UserId, r.Status })
            .HasDatabaseName("ix_hotel_requests_user_id_status");

        builder.HasIndex(r => r.OrderId)
            .HasDatabaseName("ix_hotel_requests_order_id")
            .IsUnique()
            .HasFilter("\"order_id\" IS NOT NULL");

        builder.HasIndex(r => new { r.UserId, r.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ix_hotel_requests_user_id_idempotency_key");

        builder.HasIndex(r => new { r.Status, r.ExpireAt })
            .HasDatabaseName("ix_hotel_requests_status_expire_at");
    }
}
