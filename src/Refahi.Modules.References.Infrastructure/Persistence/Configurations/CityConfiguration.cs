using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.References.Domain.Entities;

namespace Refahi.Modules.References.Infrastructure.Persistence.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ProvinceId).IsRequired();
        builder.Property(c => c.SortOrder).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => new { c.ProvinceId, c.Slug }).IsUnique();
        builder.HasIndex(c => c.ProvinceId);
        builder.HasIndex(c => c.SortOrder);

        // Province navigation
        builder.HasOne(c => c.Province)
            .WithMany()
            .HasForeignKey(c => c.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
