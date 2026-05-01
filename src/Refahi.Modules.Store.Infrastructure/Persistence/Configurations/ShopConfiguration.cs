using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Slug).HasMaxLength(200).IsRequired();
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.CoverImageUrl).HasMaxLength(500);
        
        // Location
        builder.Property(s => s.ProvinceId);
        builder.Property(s => s.CityId);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Latitude);
        builder.Property(s => s.Longitude);
        
        // Contact info
        builder.Property(s => s.ManagerName).HasMaxLength(200);
        builder.Property(s => s.ManagerPhone).HasMaxLength(20);
        builder.Property(s => s.RepresentativeName).HasMaxLength(200);
        builder.Property(s => s.RepresentativePhone).HasMaxLength(20);
        builder.Property(s => s.ContactPhone).HasMaxLength(20);
        
        builder.Property(s => s.Description).HasMaxLength(2000);

        builder.Property(s => s.ShopType).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.SupplierId).IsRequired();
        builder.Property(s => s.IsPopular).IsRequired();
        builder.Property(s => s.DeliveredOrdersCount).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => s.Slug).IsUnique();
        builder.HasIndex(s => s.SupplierId);
        builder.HasIndex(s => s.CityId);
        builder.HasIndex(s => s.ProvinceId);

    }
}
