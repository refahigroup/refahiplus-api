using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class RiskVelocityCounterConfiguration : IEntityTypeConfiguration<RiskVelocityCounter>
{
    public void Configure(EntityTypeBuilder<RiskVelocityCounter> b)
    {
        b.ToTable("risk_velocity_counters", t =>
        {
            t.HasCheckConstraint("ck_risk_counters_scope", "scope_type IN (1,2)");
            t.HasCheckConstraint("ck_risk_counters_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
            t.HasCheckConstraint("ck_risk_counters_values", "accepted_count >= 0 AND accepted_amount_minor >= 0");
            t.HasCheckConstraint("ck_risk_counters_version", "version > 0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("risk_velocity_counter_id");
        b.Property(x => x.ScopeType).HasColumnName("scope_type").HasConversion<short>();
        b.Property(x => x.ScopeId).HasColumnName("scope_id");
        b.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.BucketStart).HasColumnName("bucket_start");
        b.Property(x => x.AcceptedCount).HasColumnName("accepted_count");
        b.Property(x => x.AcceptedAmountMinor).HasColumnName("accepted_amount_minor");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.OperationType, x.BucketStart }).IsUnique()
            .HasDatabaseName("ux_risk_counters_scope_operation_bucket");
    }
}

public sealed class RiskReservationConfiguration : IEntityTypeConfiguration<RiskReservation>
{
    public void Configure(EntityTypeBuilder<RiskReservation> b)
    {
        b.ToTable("risk_reservations", t =>
        {
            t.HasCheckConstraint("ck_risk_reservations_amount", "amount_minor >= 0");
            t.HasCheckConstraint("ck_risk_reservations_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("risk_reservation_id");
        b.Property(x => x.CounterId).HasColumnName("risk_velocity_counter_id");
        b.Property(x => x.ScopeType).HasColumnName("scope_type").HasConversion<short>();
        b.Property(x => x.ScopeId).HasColumnName("scope_id");
        b.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.ResourceId).HasColumnName("resource_id");
        b.Property(x => x.AmountMinor).HasColumnName("amount_minor");
        b.Property(x => x.BucketStart).HasColumnName("bucket_start");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ReleasedAt).HasColumnName("released_at");
        b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.OperationType, x.ResourceId }).IsUnique()
            .HasDatabaseName("ux_risk_reservations_resource");
        b.HasOne<RiskVelocityCounter>().WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RiskDecisionConfiguration : IEntityTypeConfiguration<RiskDecisionRecord>
{
    public void Configure(EntityTypeBuilder<RiskDecisionRecord> b)
    {
        b.ToTable("risk_decisions", t =>
        {
            t.HasCheckConstraint("ck_risk_decisions_decision", "decision IN (1,2)");
            t.HasCheckConstraint("ck_risk_decisions_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
            t.HasCheckConstraint("ck_risk_decisions_values", "(current_value IS NULL OR current_value >= 0) AND (limit_value IS NULL OR limit_value > 0)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("risk_decision_id");
        b.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.MerchantId).HasColumnName("merchant_id"); b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id"); b.Property(x => x.ResourceId).HasColumnName("resource_id");
        b.Property(x => x.RuleCode).HasColumnName("rule_code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Decision).HasColumnName("decision").HasConversion<short>();
        b.Property(x => x.CurrentValue).HasColumnName("current_value"); b.Property(x => x.LimitValue).HasColumnName("limit_value");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at"); b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        b.HasIndex(x => new { x.ResourceId, x.OccurredAt }).HasDatabaseName("ix_risk_decisions_resource_date");
        b.HasIndex(x => new { x.RuleCode, x.OccurredAt }).HasDatabaseName("ix_risk_decisions_rule_date");
    }
}
