using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Entities;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public class SupplierLinkConfiguration : IEntityTypeConfiguration<SupplierLink>
{
    public void Configure(EntityTypeBuilder<SupplierLink> builder)
    {
        builder.ToTable("supplier_links");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.SupplierId).IsRequired();
        builder.Property(l => l.Type).IsRequired().HasColumnType("smallint");
        builder.Property(l => l.Url).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Label).HasMaxLength(200);
        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasIndex(l => l.SupplierId);
    }
}
