using Microsoft.EntityFrameworkCore;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;

public class PaymentGatewayDbContext : DbContext
{
    public PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options) : base(options)
    {
    }

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

            b.Property(x => x.ReturnBaseUrl).HasColumnName("return_base_url").HasMaxLength(500).IsRequired();
            b.Property(x => x.SucceededCallbackUrl).HasColumnName("succeeded_callback_url").HasMaxLength(500);
            b.Property(x => x.FailedCallbackUrl).HasColumnName("failed_callback_url").HasMaxLength(500);

            b.Property(x => x.ProviderToken).HasColumnName("provider_token").HasMaxLength(1000);
            b.Property(x => x.ProviderRefNum).HasColumnName("provider_ref_num").HasMaxLength(100);
            b.Property(x => x.ProviderTraceNo).HasColumnName("provider_trace_no").HasMaxLength(100);
            b.Property(x => x.ProviderSecurePan).HasColumnName("provider_secure_pan").HasMaxLength(50);
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

            b.HasIndex(x => x.UserId).HasDatabaseName("idx_pg_sessions_user");
            b.HasIndex(x => x.Status).HasDatabaseName("idx_pg_sessions_status");
            b.HasIndex(x => x.InitiatedAt).HasDatabaseName("idx_pg_sessions_initiated_at");

            b.Ignore(x => x.DomainEvents);
        });
    }
}
