using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> b)
    {
        b.ToTable("reconciliation_runs", t =>
        {
            t.HasCheckConstraint("ck_reconciliation_runs_kind", "kind BETWEEN 1 AND 6");
            t.HasCheckConstraint("ck_reconciliation_runs_status", "status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_reconciliation_runs_counts", "scanned_count >= 0 AND finding_count >= 0 AND auto_remediation_count >= 0 AND error_count >= 0");
            t.HasCheckConstraint("ck_reconciliation_runs_version", "version > 0");
            t.HasCheckConstraint("ck_reconciliation_runs_completion", "(status = 1 AND completed_at IS NULL) OR (status <> 1 AND completed_at IS NOT NULL)");
            t.HasCheckConstraint("ck_reconciliation_runs_range", "range_from IS NULL OR range_to IS NULL OR range_from <= range_to");
            t.HasCheckConstraint("ck_reconciliation_runs_lease", "lease_until > started_at");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("reconciliation_run_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<short>();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.WorkerInstanceId).HasColumnName("worker_instance_id").HasMaxLength(100).IsRequired();
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.RangeFrom).HasColumnName("range_from");
        b.Property(x => x.RangeTo).HasColumnName("range_to");
        b.Property(x => x.DryRun).HasColumnName("dry_run");
        b.Property(x => x.ScannedCount).HasColumnName("scanned_count");
        b.Property(x => x.FindingCount).HasColumnName("finding_count");
        b.Property(x => x.AutoRemediationCount).HasColumnName("auto_remediation_count");
        b.Property(x => x.ErrorCount).HasColumnName("error_count");
        b.Property(x => x.Cursor).HasColumnName("cursor").HasMaxLength(500);
        b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.Kind, x.StartedAt }).IsDescending(false, true).HasDatabaseName("ix_reconciliation_runs_kind_started");
        b.HasIndex(x => x.Kind).IsUnique().HasFilter("status = 1").HasDatabaseName("ux_reconciliation_runs_active_kind");
    }
}

public sealed class ReconciliationFindingConfiguration : IEntityTypeConfiguration<ReconciliationFinding>
{
    public void Configure(EntityTypeBuilder<ReconciliationFinding> b)
    {
        b.ToTable("reconciliation_findings", t =>
        {
            t.HasCheckConstraint("ck_reconciliation_findings_kind", "kind BETWEEN 1 AND 6");
            t.HasCheckConstraint("ck_reconciliation_findings_severity", "severity BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_reconciliation_findings_status", "status BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_reconciliation_findings_resolution", "resolution_category BETWEEN 0 AND 5");
            t.HasCheckConstraint("ck_reconciliation_findings_observation", "observation_count > 0 AND first_observed_at <= last_observed_at");
            t.HasCheckConstraint("ck_reconciliation_findings_resolved", "(status = 3 AND resolved_at IS NOT NULL) OR (status <> 3 AND resolved_at IS NULL)");
            t.HasCheckConstraint("ck_reconciliation_findings_acknowledged", "status <> 2 OR (acknowledged_at IS NOT NULL AND acknowledged_by IS NOT NULL)");
            t.HasCheckConstraint("ck_reconciliation_findings_remediation", "auto_remediation_attempt_count >= 0");
            t.HasCheckConstraint("ck_reconciliation_findings_version", "version > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("reconciliation_finding_id");
        b.Property(x => x.ReconciliationRunId).HasColumnName("reconciliation_run_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<short>();
        b.Property(x => x.FindingCode).HasColumnName("finding_code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Severity).HasColumnName("severity").HasConversion<short>();
        b.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.ResourceId).HasColumnName("resource_id");
        b.Property(x => x.MerchantId).HasColumnName("merchant_id");
        b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.FirstObservedAt).HasColumnName("first_observed_at");
        b.Property(x => x.LastObservedAt).HasColumnName("last_observed_at");
        b.Property(x => x.ObservationCount).HasColumnName("observation_count");
        b.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        b.Property(x => x.ResolutionCategory).HasColumnName("resolution_category").HasConversion<short>();
        b.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        b.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by").HasMaxLength(100);
        b.Property(x => x.AcknowledgementNote).HasColumnName("acknowledgement_note").HasMaxLength(500);
        b.Property(x => x.AutoRemediationAction).HasColumnName("auto_remediation_action").HasMaxLength(100);
        b.Property(x => x.AutoRemediationAttemptCount).HasColumnName("auto_remediation_attempt_count");
        b.Property(x => x.LastAutoRemediationAt).HasColumnName("last_auto_remediation_at");
        b.Property(x => x.NextAutoRemediationAt).HasColumnName("next_auto_remediation_at");
        b.Property(x => x.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").HasMaxLength(8192).IsRequired();
        b.Property(x => x.EvidenceHash).HasColumnName("evidence_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasOne<ReconciliationRun>().WithMany().HasForeignKey(x => x.ReconciliationRunId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.FindingCode, x.ResourceType, x.ResourceId }).IsUnique()
            .HasFilter("status <> 3").HasDatabaseName("ux_reconciliation_findings_active_logical");
        b.HasIndex(x => new { x.Status, x.Severity, x.LastObservedAt }).HasDatabaseName("ix_reconciliation_findings_status_severity_date");
        b.HasIndex(x => new { x.ResourceType, x.ResourceId, x.LastObservedAt }).HasDatabaseName("ix_reconciliation_findings_resource");
        b.HasIndex(x => new { x.Severity, x.FirstObservedAt }).HasFilter("status <> 3 AND severity = 3")
            .HasDatabaseName("ix_reconciliation_findings_open_critical");
    }
}
