using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        builder.Property(b => b.Title).HasMaxLength(200).IsRequired();
        builder.Property(b => b.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(b => b.LinkUrl).HasMaxLength(500);
        builder.Property(b => b.BannerType).IsRequired();
        builder.Property(b => b.SortOrder).IsRequired();
        builder.Property(b => b.IsActive).IsRequired();
        builder.Property(b => b.StartDate);
        builder.Property(b => b.EndDate);

        builder.HasIndex(b => b.IsActive);
    }
}
