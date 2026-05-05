using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Entities;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id");

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(30)
            .HasColumnName("order_number");

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("ix_orders_order_number");

        builder.Property(o => o.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(o => o.TotalAmountMinor)
            .IsRequired()
            .HasColumnName("total_amount_minor");

        builder.Property(o => o.DiscountAmountMinor)
            .IsRequired()
            .HasDefaultValue(0L)
            .HasColumnName("discount_amount_minor");

        builder.Property(o => o.ShippingFeeMinor)
            .IsRequired()
            .HasDefaultValue(0L)
            .HasColumnName("shipping_fee_minor");

        builder.Property(o => o.DiscountCode)
            .HasMaxLength(50)
            .HasColumnName("discount_code");

        builder.Property(o => o.DiscountCodeAmountMinor)
            .IsRequired()
            .HasDefaultValue(0L)
            .HasColumnName("discount_code_amount_minor");

        builder.Property(o => o.FinalAmountMinor)
            .IsRequired()
            .HasColumnName("final_amount_minor");

        builder.Property(o => o.ShippingAddressId)
            .HasColumnName("shipping_address_id");

        builder.Property(o => o.ShippingAddressSnapshotJson)
            .HasColumnType("jsonb")
            .HasColumnName("shipping_address_snapshot");

        builder.Property(o => o.DeliveryDate)
            .HasColumnType("date")
            .HasColumnName("delivery_date");

        builder.Property(o => o.DeliveryTimeSlot)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(Refahi.Modules.Orders.Domain.Enums.DeliveryTimeSlot.None)
            .HasColumnName("delivery_time_slot");

        builder.Property(o => o.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("IRR")
            .HasColumnName("currency");

        builder.Property(o => o.Status)
            .IsRequired()
            .HasColumnType("smallint")
            .HasColumnName("status");

        builder.Property(o => o.PaymentState)
            .IsRequired()
            .HasColumnType("smallint")
            .HasColumnName("payment_state");

        builder.Property(o => o.PaymentIntentId)
            .HasColumnName("payment_intent_id");

        builder.Property(o => o.PaymentId)
            .HasColumnName("payment_id");

        builder.Property(o => o.SourceModule)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("source_module");

        builder.Property(o => o.SourceReferenceId)
            .IsRequired()
            .HasColumnName("source_reference_id");

        builder.Property(o => o.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("idempotency_key");

        builder.HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_orders_idempotency_key");

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(o => o.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        // Optimistic Concurrency via PostgreSQL system column xmin (no migration needed — system column)
        builder.Property(o => o.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(o => o.UserId)
            .HasDatabaseName("ix_orders_user_id");

        builder.HasIndex(o => new { o.SourceModule, o.SourceReferenceId })
            .HasDatabaseName("ix_orders_source");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_orders_status");

        // Navigation — use backing field "_items" via property access mode
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // DomainEvents is an in-memory collection — EF must not map it
        builder.Ignore(o => o.DomainEvents);
    }
}
