using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Aggregates;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasColumnType("smallint");

        builder.Property(s => s.FirstName).HasMaxLength(100);
        builder.Property(s => s.LastName).HasMaxLength(100);
        builder.Property(s => s.CompanyName).HasMaxLength(200);
        builder.Property(s => s.BrandName).HasMaxLength(200);
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.NationalId).HasMaxLength(20);
        builder.Property(s => s.EconomicCode).HasMaxLength(20);

        // Location — no FK constraints across modules
        builder.Property(s => s.ProvinceId);
        builder.Property(s => s.CityId);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Latitude);
        builder.Property(s => s.Longitude);

        // Contact
        builder.Property(s => s.MobileNumber).HasMaxLength(20);
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);
        builder.Property(s => s.RepresentativeName).HasMaxLength(150);
        builder.Property(s => s.RepresentativePhone).HasMaxLength(20);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasColumnType("smallint");

        builder.Property(s => s.StatusNote).HasMaxLength(500);
        builder.Property(s => s.IsDeleted).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(s => new { s.Status, s.IsDeleted });
        builder.HasIndex(s => new { s.ProvinceId, s.CityId });

        // Unique filtered: NationalId where not null and not deleted
        builder.HasIndex(s => s.NationalId)
            .IsUnique()
            .HasFilter("\"NationalId\" IS NOT NULL AND \"IsDeleted\" = false");

        // Children via private backing fields
        builder.HasMany(s => s.Links)
            .WithOne()
            .HasForeignKey("SupplierId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Links).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Attachments)
            .WithOne()
            .HasForeignKey("SupplierId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
