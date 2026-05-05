using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Orders.Domain.Entities;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.OrderId)
            .IsRequired()
            .HasColumnName("order_id");

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("title");

        builder.Property(i => i.UnitPriceMinor)
            .IsRequired()
            .HasColumnName("unit_price_minor");

        builder.Property(i => i.Quantity)
            .IsRequired()
            .HasColumnName("quantity");

        builder.Property(i => i.DiscountAmountMinor)
            .IsRequired()
            .HasDefaultValue(0L)
            .HasColumnName("discount_amount_minor");

        builder.Property(i => i.FinalPriceMinor)
            .IsRequired()
            .HasColumnName("final_price_minor");

        builder.Property(i => i.SourceModule)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("source_module");

        builder.Property(i => i.SourceItemId)
            .IsRequired()
            .HasColumnName("source_item_id");

        builder.Property(i => i.CategoryCode)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("category_code");

        // Tags stored as PostgreSQL text[]
        builder.Property(i => i.Tags)
            .HasColumnType("text[]")
            .HasColumnName("tags");

        // MetadataJson stored as PostgreSQL jsonb
        builder.Property(i => i.MetadataJson)
            .HasColumnType("jsonb")
            .HasColumnName("metadata");

        builder.Property(i => i.DeliveryMethod)
            .IsRequired()
            .HasConversion<short>()
            .HasDefaultValue(Refahi.Modules.Orders.Domain.Enums.DeliveryMethod.None)
            .HasColumnName("delivery_method");

        builder.Property(i => i.SortOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("sort_order");

        // Indexes
        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("ix_order_items_order_id");

        builder.HasIndex(i => i.CategoryCode)
            .HasDatabaseName("ix_order_items_category");
    }
}
