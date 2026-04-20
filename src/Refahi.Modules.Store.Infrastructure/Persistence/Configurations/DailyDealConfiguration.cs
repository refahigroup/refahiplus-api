using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class DailyDealConfiguration : IEntityTypeConfiguration<DailyDeal>
{
    public void Configure(EntityTypeBuilder<DailyDeal> builder)
    {
        builder.ToTable("daily_deals");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();

        builder.Property(d => d.ModuleId).IsRequired();
        builder.Property(d => d.ProductId).IsRequired();
        builder.Property(d => d.DiscountPercent).IsRequired();
        builder.Property(d => d.StartTime).IsRequired();
        builder.Property(d => d.EndTime).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();

        builder.HasIndex(d => d.ModuleId);
        builder.HasIndex(d => d.ProductId);
        builder.HasIndex(d => d.IsActive);
    }
}
