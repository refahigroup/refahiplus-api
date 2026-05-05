using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners", t =>
        {
            t.HasCheckConstraint(
                "CK_banners_owner_xor",
                "(\"ModuleId\" IS NULL) <> (\"ShopId\" IS NULL)");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        builder.Property(b => b.ModuleId);
        builder.Property(b => b.ShopId);
        builder.Property(b => b.Title).HasMaxLength(200).IsRequired();
        builder.Property(b => b.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(b => b.LinkUrl).HasMaxLength(500);
        builder.Property(b => b.BannerType).IsRequired();
        builder.Property(b => b.SortOrder).IsRequired();
        builder.Property(b => b.IsActive).IsRequired();
        builder.Property(b => b.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(b => b.StartDate);
        builder.Property(b => b.EndDate);

        builder.HasOne<StoreModule>()
            .WithMany()
            .HasForeignKey(b => b.ModuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(b => b.ShopId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(b => b.ModuleId)
            .HasFilter("\"ModuleId\" IS NOT NULL");
        builder.HasIndex(b => b.ShopId)
            .HasFilter("\"ShopId\" IS NOT NULL");
        builder.HasIndex(b => b.IsActive);
        builder.HasIndex(b => b.IsDeleted);
    }
}
