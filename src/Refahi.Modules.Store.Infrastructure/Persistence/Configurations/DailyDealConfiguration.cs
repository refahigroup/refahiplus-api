using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class DailyDealConfiguration : IEntityTypeConfiguration<DailyDeal>
{
    public void Configure(EntityTypeBuilder<DailyDeal> builder)
    {
        builder.ToTable("daily_deals", t =>
        {
            t.HasCheckConstraint(
                "CK_daily_deals_owner_xor",
                "(\"ModuleId\" IS NULL) <> (\"ShopId\" IS NULL)");
        });

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();

        builder.Property(d => d.ModuleId);
        builder.Property(d => d.ShopId);
        builder.Property(d => d.ProductId).IsRequired();
        builder.Property(d => d.DiscountPercent).IsRequired();
        builder.Property(d => d.StartTime).IsRequired();
        builder.Property(d => d.EndTime).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();

        builder.HasOne<StoreModule>()
            .WithMany()
            .HasForeignKey(d => d.ModuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(d => d.ShopId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(d => d.ModuleId)
            .HasFilter("\"ModuleId\" IS NOT NULL");
        builder.HasIndex(d => d.ShopId)
            .HasFilter("\"ShopId\" IS NOT NULL");
        builder.HasIndex(d => d.ProductId);
        builder.HasIndex(d => d.IsActive);
    }
}
