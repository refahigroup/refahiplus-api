using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Entities;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public class AgreementProductConfiguration : IEntityTypeConfiguration<AgreementProduct>
{
    public void Configure(EntityTypeBuilder<AgreementProduct> builder)
    {
        builder.ToTable("agreement_products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AgreementId).IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description).HasMaxLength(1000);

        builder.Property(p => p.CategoryId);

        builder.Property(p => p.ProductType).IsRequired();
        builder.Property(p => p.DeliveryType).IsRequired();
        builder.Property(p => p.SalesModel).IsRequired();

        builder.Property(p => p.CommissionPercent)
            .IsRequired()
            .HasColumnType("numeric(5,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.IsDeleted).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(p => p.AgreementId);
        builder.HasIndex(p => new { p.AgreementId, p.IsDeleted });
        // Catalog display filter: WHERE CategoryId = ANY(@ids) AND IsDeleted = false
        builder.HasIndex(p => new { p.CategoryId, p.IsDeleted });
    }
}
