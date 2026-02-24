using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallets.Infrastructure.Persistence.Migrations;

public partial class InitWalletSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "wallets");

        migrationBuilder.CreateTable(
            name: "wallets",
            schema: "wallets",
            columns: table => new
            {
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_type = table.Column<short>(type: "smallint", nullable: false),
                owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_type = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_wallets", x => x.wallet_id);
            });

        migrationBuilder.CreateTable(
            name: "ledger_entries",
            schema: "wallets",
            columns: table => new
            {
                ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_type = table.Column<short>(type: "smallint", nullable: false),
                entry_type = table.Column<short>(type: "smallint", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                related_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                relation_type = table.Column<short>(type: "smallint", nullable: false),
                external_reference = table.Column<string>(type: "text", nullable: true),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ledger_entries", x => x.ledger_entry_id);
                table.ForeignKey(
                    name: "fk_ledger_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ledger_related",
                    column: x => x.related_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "wallet_balances",
            schema: "wallets",
            columns: table => new
            {
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                available_minor = table.Column<long>(type: "bigint", nullable: false),
                pending_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                last_ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_wallet_balances", x => x.wallet_id);
                table.ForeignKey(
                    name: "fk_balances_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_balances_last_ledger",
                    column: x => x.last_ledger_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "idempotency_keys",
            schema: "wallets",
            columns: table => new
            {
                idempotency_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                idempotency_key = table.Column<string>(type: "text", nullable: false),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_type = table.Column<short>(type: "smallint", nullable: false),
                request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                result_ledger_entry_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "array[]::uuid[]"),
                result_balance_available_minor = table.Column<long>(type: "bigint", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                error_code = table.Column<string>(type: "text", nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_keys", x => x.idempotency_id);
                table.ForeignKey(
                    name: "fk_idem_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_wallets_owner",
            schema: "wallets",
            table: "wallets",
            column: "owner_id");

        migrationBuilder.CreateIndex(
            name: "idx_wallets_currency",
            schema: "wallets",
            table: "wallets",
            column: "currency");

        migrationBuilder.CreateIndex(
            name: "idx_ledger_wallet",
            schema: "wallets",
            table: "ledger_entries",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "idx_ledger_wallet_created_at",
            schema: "wallets",
            table: "ledger_entries",
            columns: new[] { "wallet_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_ledger_operation",
            schema: "wallets",
            table: "ledger_entries",
            column: "operation_id");

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_wallet_key_optype",
            schema: "wallets",
            table: "idempotency_keys",
            columns: new[] { "wallet_id", "idempotency_key", "operation_type" },
            unique: true);

        // DB-level append-only enforcement for ledger_entries.
        migrationBuilder.Sql(@"
create or replace function wallets.fn_ledger_entries_append_only()
returns trigger as $$
begin
  raise exception 'ledger_entries is append-only';
end;
$$ language plpgsql;

drop trigger if exists trg_ledger_entries_no_update on wallets.ledger_entries;
drop trigger if exists trg_ledger_entries_no_delete on wallets.ledger_entries;

create trigger trg_ledger_entries_no_update
  before update on wallets.ledger_entries
  for each row execute function wallets.fn_ledger_entries_append_only();

create trigger trg_ledger_entries_no_delete
  before delete on wallets.ledger_entries
  for each row execute function wallets.fn_ledger_entries_append_only();
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
drop trigger if exists trg_ledger_entries_no_update on wallets.ledger_entries;
drop trigger if exists trg_ledger_entries_no_delete on wallets.ledger_entries;
drop function if exists wallets.fn_ledger_entries_append_only();
");

        migrationBuilder.DropTable(name: "idempotency_keys", schema: "wallets");
        migrationBuilder.DropTable(name: "wallet_balances", schema: "wallets");
        migrationBuilder.DropTable(name: "ledger_entries", schema: "wallets");
        migrationBuilder.DropTable(name: "wallets", schema: "wallets");
    }
}
