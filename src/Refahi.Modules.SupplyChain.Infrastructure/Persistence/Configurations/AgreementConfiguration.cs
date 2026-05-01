using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Aggregates;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> builder)
    {
        builder.ToTable("agreements");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AgreementNo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.AgreementType)
            .IsRequired()
            .HasColumnType("smallint");

        // SupplierId — FK to same schema (no cross-module constraint)
        builder.Property(a => a.SupplierId).IsRequired();

        builder.Property(a => a.FromDate).IsRequired();
        builder.Property(a => a.ToDate).IsRequired();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasColumnType("smallint");

        builder.Property(a => a.StatusNote).HasMaxLength(500);
        builder.Property(a => a.IsDeleted).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(a => new { a.SupplierId, a.Status });
        builder.HasIndex(a => new { a.Status, a.IsDeleted });
        // Catalog display filter: WHERE SupplierId = x AND Status = Approved AND ToDate >= now
        builder.HasIndex(a => new { a.SupplierId, a.Status, a.ToDate });

        // Unique filtered: AgreementNo where not deleted
        builder.HasIndex(a => a.AgreementNo)
            .IsUnique()
            .HasFilter("\"AgreementNo\" IS NOT NULL AND \"IsDeleted\" = false");

        // Navigation: Supplier (read-only, intra-module)
        builder.HasOne(a => a.Supplier)
            .WithMany()
            .HasForeignKey(a => a.SupplierId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Products collection via private backing field
        builder.HasMany(a => a.Products)
            .WithOne()
            .HasForeignKey("AgreementId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Products).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
