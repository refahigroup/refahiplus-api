using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class StoreOrderItemConfiguration : IEntityTypeConfiguration<StoreOrderItem>
{
    public void Configure(EntityTypeBuilder<StoreOrderItem> builder)
    {
        builder.ToTable("store_order_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductTitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.VariantTitle).HasMaxLength(300);
        builder.Property(x => x.SessionTitle).HasMaxLength(300);
        builder.Property(x => x.CategoryCode).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        builder.Property(x => x.SalesChannel).HasConversion<short>();
        builder.Property(x => x.ProductType).HasConversion<short>();
        builder.Property(x => x.SalesModel).HasConversion<short>();
        builder.Property(x => x.FulfillmentMethod).HasConversion<short>();
        builder.Property(x => x.VoucherSourceTitle).HasMaxLength(200);
        builder.Property(x => x.VoucherSourceType).HasConversion<short>();
        builder.Property(x => x.VoucherRedemptionMode).HasConversion<short>();
        builder.HasIndex(x => x.StoreOrderId);
        builder.HasIndex(x => x.OfferId).HasFilter("\"OfferId\" IS NOT NULL");
        builder.HasIndex(x => x.VoucherSourceId).HasFilter("\"VoucherSourceId\" IS NOT NULL");
    }
}
