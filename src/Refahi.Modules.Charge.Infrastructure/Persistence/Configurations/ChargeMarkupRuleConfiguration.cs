using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Charge.Domain.Aggregates;
namespace Refahi.Modules.Charge.Infrastructure.Persistence.Configurations;
public sealed class ChargeMarkupRuleConfiguration : IEntityTypeConfiguration<ChargeMarkupRule>
{
    public void Configure(EntityTypeBuilder<ChargeMarkupRule> b)
    {
        b.ToTable("charge_markup_rules"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Operator).HasColumnName("operator").HasConversion<short?>(); b.Property(x => x.ServiceType).HasColumnName("service_type").HasConversion<short?>();
        b.Property(x => x.Percent).HasColumnName("percent").HasPrecision(9, 4); b.Property(x => x.FixedAmountMinor).HasColumnName("fixed_amount_minor");
        b.Property(x => x.EffectiveFrom).HasColumnName("effective_from"); b.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        b.Property(x => x.IsActive).HasColumnName("is_active"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.RowVersion).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken().IsRowVersion();
        b.HasIndex(x => new { x.IsActive, x.Operator, x.ServiceType, x.EffectiveFrom }).HasDatabaseName("ix_charge_markup_scope");
    }
}
