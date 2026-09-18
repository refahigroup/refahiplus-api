using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> b)
    {
        b.ToTable("webhook_events", t =>
        {
            t.HasCheckConstraint("ck_webhook_events_type", "event_type IN ('payment.succeeded','payment.failed','payment.canceled','payment.expired','refund.succeeded','refund.failed')");
            t.HasCheckConstraint("ck_webhook_events_version", "event_version > 0");
            t.HasCheckConstraint("ck_webhook_events_payload_size", "octet_length(payload_json::text) <= 32768");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("webhook_event_id");
        b.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.EventVersion).HasColumnName("event_version");
        b.Property(x => x.MerchantId).HasColumnName("merchant_id");
        b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.AggregateId).HasColumnName("aggregate_id");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.SourceKey).IsUnique().HasDatabaseName("ux_webhook_events_source_key");
        b.HasIndex(x => new { x.AggregateType, x.AggregateId, x.EventType })
            .HasDatabaseName("ix_webhook_events_aggregate");
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Terminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages", t =>
        {
            t.HasCheckConstraint("ck_outbox_messages_status", "status IN (1,2,3)");
            t.HasCheckConstraint("ck_outbox_messages_retry", "retry_count >= 0");
            t.HasCheckConstraint("ck_outbox_messages_version", "version > 0");
            t.HasCheckConstraint("ck_outbox_messages_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
            t.HasCheckConstraint("ck_outbox_messages_completion", "(status = 3 AND completed_at IS NOT NULL AND lease_owner IS NULL AND lease_until IS NULL) OR (status <> 3 AND completed_at IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("outbox_message_id");
        b.Property(x => x.WebhookEventId).HasColumnName("webhook_event_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.WebhookEventId).IsUnique().HasDatabaseName("ux_outbox_messages_webhook_event");
        b.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseUntil }).HasDatabaseName("ix_outbox_messages_dispatch");
        b.HasOne<WebhookEvent>().WithMany().HasForeignKey(x => x.WebhookEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries", t =>
        {
            t.HasCheckConstraint("ck_webhook_deliveries_status", "status IN (1,2,3,4,5)");
            t.HasCheckConstraint("ck_webhook_deliveries_attempts", "attempt_count >= 0 AND manual_resend_count >= 0");
            t.HasCheckConstraint("ck_webhook_deliveries_version", "version > 0");
            t.HasCheckConstraint("ck_webhook_deliveries_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
            t.HasCheckConstraint("ck_webhook_deliveries_delivered", "status <> 4 OR delivered_at IS NOT NULL");
            t.HasCheckConstraint("ck_webhook_deliveries_terminal_lease", "status NOT IN (4,5) OR (lease_owner IS NULL AND lease_until IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("webhook_delivery_id");
        b.Property(x => x.WebhookEventId).HasColumnName("webhook_event_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        b.Property(x => x.ManualResendCount).HasColumnName("manual_resend_count");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        b.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        b.Property(x => x.LastHttpStatusCode).HasColumnName("last_http_status_code");
        b.Property(x => x.LastFailureCode).HasColumnName("last_failure_code").HasMaxLength(100);
        b.Property(x => x.LastSecretVersionUsed).HasColumnName("last_secret_version_used");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.WebhookEventId).IsUnique().HasDatabaseName("ux_webhook_deliveries_webhook_event");
        b.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LeaseUntil }).HasDatabaseName("ix_webhook_deliveries_due");
        b.HasOne<WebhookEvent>().WithMany().HasForeignKey(x => x.WebhookEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
