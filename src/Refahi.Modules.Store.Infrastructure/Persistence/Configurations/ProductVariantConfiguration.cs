using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.ProductId).IsRequired();
        builder.Property(v => v.SKU).HasMaxLength(50);
        builder.Property(v => v.ImageUrl).HasMaxLength(500);
        builder.Property(v => v.StockCount).IsRequired();
        builder.Property(v => v.PriceMinor).IsRequired();
        builder.Property(v => v.DiscountedPriceMinor);
        builder.Property(v => v.FromDate).HasColumnType("date");
        builder.Property(v => v.ToDate).HasColumnType("date");
        builder.Property(v => v.CapacityType)
            .HasColumnType("smallint")
            .HasDefaultValue(VariantCapacityType.Unlimited)
            .IsRequired();
        builder.Property(v => v.Capacity);
        builder.Property(v => v.IsAvailable).IsRequired();

        builder.HasIndex(v => v.ProductId);
        builder.HasIndex(v => new { v.ProductId, v.CapacityType });
        builder.HasIndex(v => new { v.ProductId, v.FromDate, v.ToDate });

        builder.HasMany(v => v.Combinations)
            .WithOne()
            .HasForeignKey("ProductVariantId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Combinations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
