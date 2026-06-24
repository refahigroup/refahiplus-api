using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ShopProductVariantConfiguration : IEntityTypeConfiguration<ShopProductVariant>
{
    public void Configure(EntityTypeBuilder<ShopProductVariant> builder)
    {
        builder.ToTable("shop_product_variants");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.ShopProductId).IsRequired();
        builder.Property(v => v.ProductVariantId).IsRequired();
        builder.Property(v => v.PriceMinor).IsRequired();
        builder.Property(v => v.DiscountedPriceMinor);
        builder.Property(v => v.IsActive).IsRequired();
        builder.Property(v => v.IsDeleted).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt).IsRequired();

        builder.HasIndex(v => v.ShopProductId);
        builder.HasIndex(v => v.ProductVariantId);
        builder.HasIndex(v => v.IsDeleted);
        builder.HasIndex(v => new { v.ShopProductId, v.ProductVariantId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(v => v.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
