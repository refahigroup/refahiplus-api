using Microsoft.EntityFrameworkCore;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;

public class PaymentGatewayDbContext : DbContext
{
    public PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options)
        : base(options) { }

    public DbSet<PaymentGatewaySession> Sessions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("payment_gateway");

        modelBuilder.Entity<PaymentGatewaySession>(b =>
        {
            b.ToTable("sessions");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("session_id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
            b.Property(x => x.AmountMinor).HasColumnName("amount_minor").IsRequired();
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            b.Property(x => x.Provider)
                .HasColumnName("provider")
                .HasColumnType("smallint")
                .IsRequired();
            b.Property(x => x.Status)
                .HasColumnName("status")
                .HasColumnType("smallint")
                .IsRequired();

            b.Property(x => x.ReturnBaseUrl)
                .HasColumnName("return_base_url")
                .HasMaxLength(500)
                .IsRequired();
            b.Property(x => x.SucceededCallbackUrl)
                .HasColumnName("succeeded_callback_url")
                .HasMaxLength(500);
            b.Property(x => x.FailedCallbackUrl)
                .HasColumnName("failed_callback_url")
                .HasMaxLength(500);

            b.Property(x => x.ProviderToken).HasColumnName("provider_token").HasMaxLength(1000);
            b.Property(x => x.ProviderAttemptId)
                .HasColumnName("provider_attempt_id").HasMaxLength(150);
            b.Property(x => x.ProviderTransactionId)
                .HasColumnName("provider_transaction_id").HasMaxLength(150);
            b.Property(x => x.ProviderRefNum).HasColumnName("provider_ref_num").HasMaxLength(100);
            b.Property(x => x.ProviderTraceNo).HasColumnName("provider_trace_no").HasMaxLength(100);
            b.Property(x => x.ProviderSecurePan)
                .HasColumnName("provider_secure_pan")
                .HasMaxLength(50);
            b.Property(x => x.ProviderRawCallbackJson)
                .HasColumnName("provider_raw_callback")
                .HasColumnType("jsonb");
            b.Property(x => x.ProviderResultCode).HasColumnName("provider_result_code");
            b.Property(x => x.ProviderResultDescription)
                .HasColumnName("provider_result_description")
                .HasMaxLength(500);

            b.Property(x => x.InitiatedAt).HasColumnName("initiated_at").IsRequired();
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            b.Property(x => x.CompletedAt).HasColumnName("completed_at");

            b.Property(x => x.TopUpLedgerEntryId).HasColumnName("topup_ledger_entry_id");
            b.Property(x => x.InitiationIdempotencyKey)
                .HasColumnName("initiation_idempotency_key").HasMaxLength(150);
            b.Property(x => x.InitiationRequestHash)
                .HasColumnName("initiation_request_hash").HasMaxLength(64);
            b.Property(x => x.InitiationRequestHashVersion)
                .HasColumnName("initiation_request_hash_version");
            b.Property(x => x.ProcessingLeaseUntil).HasColumnName("processing_lease_until");
            b.Property(x => x.LastRecoveryAt).HasColumnName("last_recovery_at");
            b.Property(x => x.NextRecoveryAt).HasColumnName("next_recovery_at");
            b.Property(x => x.RecoveryAttemptCount).HasColumnName("recovery_attempt_count");
            b.Property(x => x.ReversalStatus)
                .HasColumnName("reversal_status").HasColumnType("smallint");
            b.Property(x => x.ReversalAttemptCount).HasColumnName("reversal_attempt_count");
            b.Property(x => x.NextReversalAt).HasColumnName("next_reversal_at");
            b.Property(x => x.ReversalDescription)
                .HasColumnName("reversal_description").HasMaxLength(500);
            b.Property(x => x.Version)
                .HasColumnName("version").IsConcurrencyToken();

            b.HasIndex(x => x.UserId).HasDatabaseName("idx_pg_sessions_user");
            b.HasIndex(x => x.Status).HasDatabaseName("idx_pg_sessions_status");
            b.HasIndex(x => x.InitiatedAt).HasDatabaseName("idx_pg_sessions_initiated_at");
            b.HasIndex(x => new { x.UserId, x.InitiationIdempotencyKey })
                .IsUnique()
                .HasFilter("initiation_idempotency_key IS NOT NULL")
                .HasDatabaseName("uq_pg_sessions_user_idempotency");
            b.HasIndex(x => new { x.Provider, x.ProviderAttemptId })
                .IsUnique()
                .HasFilter("provider_attempt_id IS NOT NULL")
                .HasDatabaseName("uq_pg_sessions_provider_attempt");
            b.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
                .IsUnique()
                .HasFilter("provider_transaction_id IS NOT NULL")
                .HasDatabaseName("uq_pg_sessions_provider_transaction");
            b.HasIndex(x => x.TopUpLedgerEntryId)
                .IsUnique()
                .HasFilter("topup_ledger_entry_id IS NOT NULL")
                .HasDatabaseName("uq_pg_sessions_topup_ledger");
            b.HasIndex(x => new { x.Status, x.NextRecoveryAt })
                .HasDatabaseName("idx_pg_sessions_recovery");
            b.HasIndex(x => new { x.ReversalStatus, x.NextReversalAt })
                .HasDatabaseName("idx_pg_sessions_reversal");

            b.ToTable(t =>
            {
                t.HasCheckConstraint("ck_pg_sessions_amount_positive", "amount_minor > 0");
                t.HasCheckConstraint("ck_pg_sessions_currency", "length(currency) = 3");
                t.HasCheckConstraint("ck_pg_sessions_provider", "provider IN (1, 2)");
                t.HasCheckConstraint("ck_pg_sessions_status", "status BETWEEN 1 AND 9");
                t.HasCheckConstraint("ck_pg_sessions_reversal_status", "reversal_status BETWEEN 0 AND 5");
                t.HasCheckConstraint(
                    "ck_pg_sessions_initiation_hash_pair",
                    "(initiation_request_hash IS NULL) = (initiation_request_hash_version IS NULL)");
            });

            b.Ignore(x => x.DomainEvents);
        });
    }
}
