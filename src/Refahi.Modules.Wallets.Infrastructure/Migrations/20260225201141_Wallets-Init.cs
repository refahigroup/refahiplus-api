using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalletsInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wallets");

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "wallets",
                columns: table => new
                {
                    idempotency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<short>(type: "smallint", nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    result_ledger_entry_ids = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    result_balance_available_minor = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.idempotency_id);
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
                    amount_minor = table.Column<long>(type: "bigint", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    related_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    relation_type = table.Column<short>(type: "smallint", nullable: false),
                    external_reference = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.ledger_entry_id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_entries_related_entry_id",
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
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_balances", x => x.wallet_id);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "wallets",
                columns: table => new
                {
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.wallet_id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_wallet_key_optype",
                schema: "wallets",
                table: "idempotency_keys",
                columns: new[] { "wallet_id", "idempotency_key", "operation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ledger_operation",
                schema: "wallets",
                table: "ledger_entries",
                column: "operation_id");

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
                name: "IX_ledger_entries_related_entry_id",
                schema: "wallets",
                table: "ledger_entries",
                column: "related_entry_id");

            migrationBuilder.CreateIndex(
                name: "idx_wallets_owner",
                schema: "wallets",
                table: "wallets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "wallets");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "wallets");

            migrationBuilder.DropTable(
                name: "wallet_balances",
                schema: "wallets");

            migrationBuilder.DropTable(
                name: "wallets",
                schema: "wallets");
        }
    }
}
