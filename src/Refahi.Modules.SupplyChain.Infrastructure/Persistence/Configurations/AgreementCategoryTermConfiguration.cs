using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.SupplyChain.Domain.Entities;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

public sealed class AgreementCategoryTermConfiguration
    : IEntityTypeConfiguration<AgreementCategoryTerm>
{
    public void Configure(EntityTypeBuilder<AgreementCategoryTerm> builder)
    {
        builder.ToTable("agreement_category_terms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AgreementId).IsRequired();
        builder.Property(x => x.CategoryId).IsRequired();
        builder.Property(x => x.AllowedSalesChannels).IsRequired().HasColumnType("smallint");
        builder.Property(x => x.CommissionPercent).IsRequired().HasColumnType("numeric(5,2)");
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.AgreementId);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.IsDeleted);
        builder
            .HasIndex(x => new
            {
                x.CategoryId,
                x.AllowedSalesChannels,
                x.IsDeleted,
                x.AgreementId,
            })
            .HasDatabaseName("IX_agreement_category_terms_effective_lookup");
    }
}
