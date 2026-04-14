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
        builder.Property(v => v.Size).HasMaxLength(20);
        builder.Property(v => v.Color).HasMaxLength(100);
        builder.Property(v => v.ColorHex).HasMaxLength(10);
        builder.Property(v => v.ImageUrl).HasMaxLength(500);
        builder.Property(v => v.StockCount).IsRequired();
        builder.Property(v => v.PriceAdjustment).IsRequired();
        builder.Property(v => v.IsAvailable).IsRequired();

        builder.HasIndex(v => v.ProductId);
    }
}
