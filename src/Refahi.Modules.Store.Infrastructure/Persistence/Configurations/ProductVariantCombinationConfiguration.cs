using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class ProductVariantCombinationConfiguration : IEntityTypeConfiguration<ProductVariantCombination>
{
    public void Configure(EntityTypeBuilder<ProductVariantCombination> builder)
    {
        builder.ToTable("product_variant_combinations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ProductVariantId).IsRequired();
        builder.Property(c => c.VariantAttributeId).IsRequired();
        builder.Property(c => c.VariantAttributeValueId).IsRequired();

        builder.HasIndex(c => c.ProductVariantId);
        builder.HasIndex(c => c.VariantAttributeValueId);
    }
}
