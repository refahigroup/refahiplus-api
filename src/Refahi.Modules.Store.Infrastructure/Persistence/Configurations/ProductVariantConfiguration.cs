using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

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
        builder.Property(v => v.IsAvailable).IsRequired();

        builder.HasIndex(v => v.ProductId);

        builder.HasMany(v => v.Combinations)
            .WithOne()
            .HasForeignKey("ProductVariantId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Combinations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
