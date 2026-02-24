using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;


#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    [DbContext(typeof(WalletsDbContext))]
    public partial class WalletsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasDefaultSchema("wallets")
                .HasAnnotation("ProductVersion", "10.0.0");

            modelBuilder.Entity("Wallets.Domain.Aggregates.Wallet", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnName("wallet_id")
                    .HasColumnType("uuid");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Currency")
                    .IsRequired()
                    .HasMaxLength(3)
                    .HasColumnName("currency")
                    .HasColumnType("character varying(3)");

                b.Property<Guid>("OwnerId")
                    .HasColumnName("owner_id")
                    .HasColumnType("uuid");

                b.Property<short>("OwnerType")
                    .HasColumnName("owner_type")
                    .HasColumnType("smallint");

                b.Property<short>("Status")
                    .HasColumnName("status")
                    .HasColumnType("smallint");

                b.Property<short>("WalletType")
                    .HasColumnName("wallet_type")
                    .HasColumnType("smallint");

                b.HasKey("Id")
                    .HasName("pk_wallets");

                b.HasIndex("Currency")
                    .HasDatabaseName("idx_wallets_currency");

                b.HasIndex("OwnerId")
                    .HasDatabaseName("idx_wallets_owner");

                b.ToTable("wallets", "wallets");
            });

            modelBuilder.Entity("Wallets.Domain.Aggregates.LedgerEntry", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnName("ledger_entry_id")
                    .HasColumnType("uuid");

                b.Property<long>("AmountMinor")
                    .HasColumnName("amount_minor")
                    .HasColumnType("bigint");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Currency")
                    .IsRequired()
                    .HasMaxLength(3)
                    .HasColumnName("currency")
                    .HasColumnType("character varying(3)");

                b.Property<short>("EntryType")
                    .HasColumnName("entry_type")
                    .HasColumnType("smallint");

                b.Property<DateTimeOffset>("EffectiveAt")
                    .HasColumnName("effective_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("ExternalReference")
                    .HasColumnName("external_reference")
                    .HasColumnType("text");

                b.Property<string>("MetadataJson")
                    .HasColumnName("metadata")
                    .HasColumnType("jsonb");

                b.Property<Guid>("OperationId")
                    .HasColumnName("operation_id")
                    .HasColumnType("uuid");

                b.Property<short>("OperationType")
                    .HasColumnName("operation_type")
                    .HasColumnType("smallint");

                b.Property<Guid?>("RelatedEntryId")
                    .HasColumnName("related_entry_id")
                    .HasColumnType("uuid");

                b.Property<short>("RelationType")
                    .HasColumnName("relation_type")
                    .HasColumnType("smallint");

                b.Property<Guid>("WalletId")
                    .HasColumnName("wallet_id")
                    .HasColumnType("uuid");

                b.HasKey("Id")
                    .HasName("pk_ledger_entries");

                b.HasIndex("OperationId")
                    .HasDatabaseName("idx_ledger_operation");

                b.HasIndex("WalletId")
                    .HasDatabaseName("idx_ledger_wallet");

                b.HasIndex("WalletId", "CreatedAt")
                    .HasDatabaseName("idx_ledger_wallet_created_at");

                b.ToTable("ledger_entries", "wallets");
            });

            modelBuilder.Entity("Wallets.Infrastructure.Persistence.Models.WalletBalanceRecord", b =>
            {
                b.Property<Guid>("WalletId")
                    .HasColumnName("wallet_id")
                    .HasColumnType("uuid");

                b.Property<long>("AvailableMinor")
                    .HasColumnName("available_minor")
                    .HasColumnType("bigint");

                b.Property<string>("Currency")
                    .IsRequired()
                    .HasMaxLength(3)
                    .HasColumnName("currency")
                    .HasColumnType("character varying(3)");

                b.Property<Guid?>("LastLedgerEntryId")
                    .HasColumnName("last_ledger_entry_id")
                    .HasColumnType("uuid");

                b.Property<long>("PendingMinor")
                    .HasColumnName("pending_minor")
                    .HasColumnType("bigint");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnName("updated_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<long>("Version")
                    .HasColumnName("version")
                    .HasColumnType("bigint");

                b.HasKey("WalletId")
                    .HasName("pk_wallet_balances");

                b.ToTable("wallet_balances", "wallets");
            });

            modelBuilder.Entity("Wallets.Infrastructure.Persistence.Models.IdempotencyKeyRecord", b =>
            {
                b.Property<Guid>("IdempotencyId")
                    .HasColumnName("idempotency_id")
                    .HasColumnType("uuid");

                b.Property<DateTimeOffset?>("CompletedAt")
                    .HasColumnName("completed_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("ErrorCode")
                    .HasColumnName("error_code")
                    .HasColumnType("text");

                b.Property<string>("ErrorMessage")
                    .HasColumnName("error_message")
                    .HasColumnType("text");

                b.Property<string>("IdempotencyKey")
                    .IsRequired()
                    .HasColumnName("idempotency_key")
                    .HasColumnType("text");

                b.Property<Guid>("OperationId")
                    .HasColumnName("operation_id")
                    .HasColumnType("uuid");

                b.Property<short>("OperationType")
                    .HasColumnName("operation_type")
                    .HasColumnType("smallint");

                b.Property<byte[]>("RequestHash")
                    .IsRequired()
                    .HasColumnName("request_hash")
                    .HasColumnType("bytea");

                b.Property<long?>("ResultBalanceAvailableMinor")
                    .HasColumnName("result_balance_available_minor")
                    .HasColumnType("bigint");

                b.Property<Guid[]>("ResultLedgerEntryIds")
                    .HasColumnName("result_ledger_entry_ids")
                    .HasColumnType("uuid[]");

                b.Property<short>("Status")
                    .HasColumnName("status")
                    .HasColumnType("smallint");

                b.Property<Guid>("WalletId")
                    .HasColumnName("wallet_id")
                    .HasColumnType("uuid");

                b.HasKey("IdempotencyId")
                    .HasName("pk_idempotency_keys");

                b.HasIndex("WalletId", "IdempotencyKey", "OperationType")
                    .IsUnique()
                    .HasDatabaseName("ux_idempotency_wallet_key_optype");

                b.ToTable("idempotency_keys", "wallets");
            });
        }
    }
}
