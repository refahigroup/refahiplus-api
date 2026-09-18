using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class CreditPaymentIntegrationEventConfiguration
    : IEntityTypeConfiguration<CreditPaymentIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<CreditPaymentIntegrationEvent> b)
    {
        b.ToTable("integration_events", t =>
        {
            t.HasCheckConstraint("ck_integration_events_type",
                "event_type IN ('credit-payment-captured.v1','credit-payment-refunded.v1')");
            t.HasCheckConstraint("ck_integration_events_version", "event_version > 0");
            t.HasCheckConstraint("ck_integration_events_source", "source = 'CreditPayments'");
            t.HasCheckConstraint("ck_integration_events_status", "status IN (1,2,3)");
            t.HasCheckConstraint("ck_integration_events_retry", "retry_count >= 0");
            t.HasCheckConstraint("ck_integration_events_payload_size", "octet_length(payload_json::text) <= 32768");
            t.HasCheckConstraint("ck_integration_events_version_token", "version > 0");
            t.HasCheckConstraint("ck_integration_events_lease",
                "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
            t.HasCheckConstraint("ck_integration_events_published",
                "(status = 3 AND published_at IS NOT NULL AND lease_owner IS NULL AND lease_until IS NULL) OR (status <> 3 AND published_at IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("integration_event_id");
        b.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        b.Property(x => x.EventVersion).HasColumnName("event_version");
        b.Property(x => x.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        b.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.AggregateId).HasColumnName("aggregate_id");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.PublishedAt).HasColumnName("published_at");
        b.Property(x => x.LastFailureCode).HasColumnName("last_failure_code").HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.SourceKey).IsUnique().HasDatabaseName("ux_integration_events_source_key");
        b.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseUntil })
            .HasDatabaseName("ix_integration_events_publish_due");
        b.HasIndex(x => new { x.AggregateType, x.AggregateId })
            .HasDatabaseName("ix_integration_events_aggregate");
        b.HasIndex(x => new { x.EventType, x.OccurredAt })
            .HasDatabaseName("ix_integration_events_type_occurred");
    }
}
