using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> builder)
    {
        builder.ToTable("variant_attribute_values");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VariantAttributeId).IsRequired();
        builder.Property(v => v.Value).HasMaxLength(200).IsRequired();
        builder.Property(v => v.SortOrder).IsRequired();

        builder.HasIndex(v => v.VariantAttributeId);
    }
}
