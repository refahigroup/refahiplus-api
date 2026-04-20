using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class VariantAttributeConfiguration : IEntityTypeConfiguration<VariantAttribute>
{
    public void Configure(EntityTypeBuilder<VariantAttribute> builder)
    {
        builder.ToTable("variant_attributes");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ProductId).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.SortOrder).IsRequired();

        builder.HasIndex(a => a.ProductId);

        builder.HasMany(a => a.Values)
            .WithOne()
            .HasForeignKey("VariantAttributeId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
