using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class StoreModuleConfiguration : IEntityTypeConfiguration<StoreModule>
{
    public void Configure(EntityTypeBuilder<StoreModule> builder)
    {
        builder.ToTable("modules");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Slug).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.Property(m => m.IconUrl).HasMaxLength(500);
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.SortOrder).IsRequired();

        builder.HasIndex(m => m.Slug).IsUnique();
        builder.HasIndex(m => m.IsActive);
    }
}
