using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wallets.Domain.Aggregates;
using Wallets.Domain.Enums;
using Wallets.Domain.ValueObjects;
using Wallets.Infrastructure.Persistence.Models;

namespace Wallets.Infrastructure.Persistence.Context;

public class WalletsDbContext : DbContext
{
    public WalletsDbContext(DbContextOptions<WalletsDbContext> options) : base(options)
    {
    }

    // NOTE: EF Core is used here for schema/migrations only.
    // Domain entities MUST NOT leak persistence concerns (no EF attributes in Domain).
    public DbSet<Wallet> Wallets { get; set; } = null!;
    public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;
    public DbSet<WalletBalanceRecord> WalletBalances { get; set; } = null!;
    public DbSet<IdempotencyKeyRecord> IdempotencyKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Value Object Converters
        var currencyConverter = new ValueConverter<Currency, string>(
            v => v.Code,
            v => Currency.Of(v));

        modelBuilder.Entity<Wallet>(b =>
        {
            b.ToTable("wallets", "wallets");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("wallet_id");

            b.Property(x => x.OwnerType).HasColumnName("owner_type").HasColumnType("smallint").IsRequired();
            b.Property(x => x.OwnerId).HasColumnName("owner_id").IsRequired();

            b.Property(x => x.WalletType).HasColumnName("wallet_type").HasColumnType("smallint").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").HasColumnType("smallint").IsRequired();

            // Value Object: Currency
            b.Property(x => x.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired()
                .HasConversion(currencyConverter);

            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            b.HasIndex(x => x.OwnerId).HasDatabaseName("idx_wallets_owner");
            b.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<LedgerEntry>(b =>
        {
            b.ToTable("ledger_entries", "wallets");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("ledger_entry_id");
            b.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
            b.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();

            b.Property(x => x.OperationType).HasColumnName("operation_type").HasColumnType("smallint").IsRequired();
            b.Property(x => x.EntryType).HasColumnName("entry_type").HasColumnType("smallint").IsRequired();

            // Value Object: Money (owned entity approach)
            b.OwnsOne(x => x.Money, money =>
            {
                money.Property(m => m.AmountMinor).HasColumnName("amount_minor").IsRequired();
                money.Property(m => m.Currency)
                    .HasColumnName("currency")
                    .HasMaxLength(3)
                    .IsRequired()
                    .HasConversion(currencyConverter);
            });

            b.Property(x => x.EffectiveAt).HasColumnName("effective_at").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            b.Property(x => x.RelatedEntryId).HasColumnName("related_entry_id");
            b.Property(x => x.RelationType).HasColumnName("relation_type").HasColumnType("smallint").IsRequired();

            b.Property(x => x.ExternalReference).HasColumnName("external_reference");
            b.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");

            b.HasIndex(x => x.WalletId).HasDatabaseName("idx_ledger_wallet");
            b.HasIndex(x => new { x.WalletId, x.CreatedAt }).HasDatabaseName("idx_ledger_wallet_created_at");
            b.HasIndex(x => x.OperationId).HasDatabaseName("idx_ledger_operation");
        });

        modelBuilder.Entity<WalletBalanceRecord>(b =>
        {
            b.ToTable("wallet_balances", "wallets");
            b.HasKey(x => x.WalletId);
            b.Property(x => x.WalletId).HasColumnName("wallet_id");
            b.Property(x => x.AvailableMinor).HasColumnName("available_minor").IsRequired();
            b.Property(x => x.PendingMinor).HasColumnName("pending_minor").IsRequired();
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            b.Property(x => x.LastLedgerEntryId).HasColumnName("last_ledger_entry_id");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            b.Property(x => x.Version).HasColumnName("version").IsRequired();
        });

        modelBuilder.Entity<IdempotencyKeyRecord>(b =>
        {
            b.ToTable("idempotency_keys", "wallets");
            b.HasKey(x => x.IdempotencyId);
            b.HasIndex(x => new { x.WalletId, x.IdempotencyKey, x.OperationType }).IsUnique().HasDatabaseName("ux_idempotency_wallet_key_optype");

            b.Property(x => x.IdempotencyId).HasColumnName("idempotency_id");
            b.Property(x => x.WalletId).HasColumnName("wallet_id");
            b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            b.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();
            b.Property(x => x.OperationType).HasColumnName("operation_type").HasColumnType("smallint").IsRequired();

            b.Property(x => x.RequestHash).HasColumnName("request_hash").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").HasColumnType("smallint").IsRequired();

            b.Property(x => x.ResultLedgerEntryIds).HasColumnName("result_ledger_entry_ids");
            b.Property(x => x.ResultBalanceAvailableMinor).HasColumnName("result_balance_available_minor");

            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(x => x.CompletedAt).HasColumnName("completed_at");
            b.Property(x => x.ErrorCode).HasColumnName("error_code");
            b.Property(x => x.ErrorMessage).HasColumnName("error_message");
        });

        // Ledger self relation
        modelBuilder.Entity<LedgerEntry>().HasOne<LedgerEntry>().WithMany().HasForeignKey(x => x.RelatedEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
