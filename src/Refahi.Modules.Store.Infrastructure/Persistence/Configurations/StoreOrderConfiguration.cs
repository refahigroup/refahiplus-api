using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class StoreOrderConfiguration : IEntityTypeConfiguration<StoreOrder>
{
    public void Configure(EntityTypeBuilder<StoreOrder> builder)
    {
        builder.ToTable(
            "store_orders",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_store_orders_amounts",
                    "\"OriginalAmountMinor\" >= \"FinalAmountMinor\" AND \"FinalAmountMinor\" > 0"
                );
            }
        );
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceModule).HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.InitiatorType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.OtpReferenceCode).HasMaxLength(2048);
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.SalesChannel).HasConversion<short>();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        builder.HasIndex(x => new
        {
            x.SalesChannel,
            x.ShopId,
            x.CreatedAt,
        });
        builder
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.StoreOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
