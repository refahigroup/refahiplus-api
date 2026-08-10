using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers", table =>
        {
            table.HasCheckConstraint("CK_offers_original_price", "\"OriginalPriceMinor\" > 0");
            table.HasCheckConstraint("CK_offers_discount", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
            table.HasCheckConstraint("CK_offers_window", "\"EndDateUtc\" IS NULL OR \"StartDateUtc\" < \"EndDateUtc\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalPriceMinor).IsRequired();
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.FinalPriceMinor).IsRequired();
        builder.Property(x => x.StartDateUtc).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.ShopId);
        builder.HasIndex(x => new { x.ProductId, x.ShopId, x.ProductVariantId, x.ProductSessionId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"IsDeleted\" = false AND \"EndDateUtc\" IS NULL")
            .HasDatabaseName("UX_offers_open_coordinate");
        builder.HasIndex(x => new { x.ProductId, x.ShopId, x.IsActive, x.IsDeleted, x.StartDateUtc, x.EndDateUtc });
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.ProductSession>().WithMany().HasForeignKey(x => x.ProductSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
