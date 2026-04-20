using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.References.Domain.Entities;

namespace Refahi.Modules.References.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.SortOrder).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.SortOrder);
    }
}
