using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ProductSessionConfiguration : IEntityTypeConfiguration<ProductSession>
{
    public void Configure(EntityTypeBuilder<ProductSession> builder)
    {
        builder.ToTable("product_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductId).IsRequired();
        builder.Property(s => s.Date)
            .HasColumnType("date")
            .IsRequired();
        builder.Property(s => s.StartTime)
            .HasColumnType("time")
            .IsRequired();
        builder.Property(s => s.EndTime)
            .HasColumnType("time")
            .IsRequired();
        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.Capacity).IsRequired();
        builder.Property(s => s.SoldCount).IsRequired();
        builder.Property(s => s.PriceAdjustment).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.IsCancelled).IsRequired();

        builder.Ignore(s => s.RemainingCapacity);
        builder.Ignore(s => s.IsAvailable);

        builder.HasIndex(s => s.ProductId);
        builder.HasIndex(s => new { s.ProductId, s.Date });
    }
}
