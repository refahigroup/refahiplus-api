using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Entities;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public class SupplierAttachmentConfiguration : IEntityTypeConfiguration<SupplierAttachment>
{
    public void Configure(EntityTypeBuilder<SupplierAttachment> builder)
    {
        builder.ToTable("supplier_attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.SupplierId).IsRequired();
        builder.Property(a => a.Title).HasMaxLength(150).IsRequired();
        builder.Property(a => a.FileUrl).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(300);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.SizeBytes);
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.SupplierId);
    }
}
