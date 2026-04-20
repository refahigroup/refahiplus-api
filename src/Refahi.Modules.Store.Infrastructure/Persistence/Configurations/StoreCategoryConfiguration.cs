using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class StoreCategoryConfiguration : IEntityTypeConfiguration<StoreCategory>
{
    public void Configure(EntityTypeBuilder<StoreCategory> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.ModuleId).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CategoryCode).HasMaxLength(100).IsRequired();
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.ParentId);
        builder.Property(c => c.SortOrder).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.CategoryCode).IsUnique();
        builder.HasIndex(c => c.ModuleId);
        builder.HasIndex(c => c.ParentId);

        // Self-referencing navigation for Children (loaded explicitly when needed)
        builder.HasMany(c => c.Children)
            .WithOne()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(c => c.Children).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
