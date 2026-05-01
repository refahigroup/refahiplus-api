using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ShopProductConfiguration : IEntityTypeConfiguration<ShopProduct>
{
    public void Configure(EntityTypeBuilder<ShopProduct> builder)
    {
        builder.ToTable("shop_products");

        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.ShopId).IsRequired();
        builder.Property(sp => sp.ProductId).IsRequired();
        builder.Property(sp => sp.Price).IsRequired();
        builder.Property(sp => sp.DiscountedPrice).IsRequired();
        builder.Property(sp => sp.Description).HasMaxLength(2000);
        builder.Property(sp => sp.IsActive).IsRequired();
        builder.Property(sp => sp.IsDeleted).IsRequired();
        builder.Property(sp => sp.CreatedAt).IsRequired();
        builder.Property(sp => sp.UpdatedAt).IsRequired();

        // Unique constraint: one product can only be linked to a shop once (ignoring soft-deleted)
        builder.HasIndex(sp => new { sp.ShopId, sp.ProductId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(sp => sp.ProductId);
        builder.HasIndex(sp => sp.ShopId);
        builder.HasIndex(sp => sp.IsDeleted);
    }
}
