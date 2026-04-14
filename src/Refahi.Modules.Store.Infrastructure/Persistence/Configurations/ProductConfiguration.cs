using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ShopId).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(5000);
        builder.Property(p => p.PriceMinor).IsRequired();
        builder.Property(p => p.DiscountedPriceMinor);
        builder.Property(p => p.DiscountPercent);
        builder.Property(p => p.ProductType).IsRequired();
        builder.Property(p => p.DeliveryType).IsRequired();
        builder.Property(p => p.SalesModel).IsRequired();
        builder.Property(p => p.StockCount).IsRequired();
        builder.Property(p => p.IsAvailable).IsRequired();
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.Area).HasMaxLength(100);
        builder.Property(p => p.CategoryId).IsRequired();
        builder.Property(p => p.CategoryCode).HasMaxLength(100).IsRequired();
        builder.Property(p => p.IsDeleted).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.ShopId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.IsDeleted);

        // Owned collections via private backing fields
        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Specifications)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Specifications).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Sessions)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Sessions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
