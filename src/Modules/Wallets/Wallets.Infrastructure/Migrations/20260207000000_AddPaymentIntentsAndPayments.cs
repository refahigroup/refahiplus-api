using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallets.Infrastructure.Persistence.Migrations;

public partial class AddPaymentIntentsAndPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ================================================================
        // PAYMENT INTENTS TABLE
        // ================================================================
        migrationBuilder.CreateTable(
            name: "payment_intents",
            schema: "wallets",
            columns: table => new
            {
                intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                idempotency_key = table.Column<string>(type: "text", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_payment_intents", x => x.intent_id);
            });

        // ================================================================
        // PAYMENT INTENT ALLOCATIONS TABLE
        // ================================================================
        migrationBuilder.CreateTable(
            name: "payment_intent_allocations",
            schema: "wallets",
            columns: table => new
            {
                allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_payment_intent_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "fk_intent_alloc_intent",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_intent_alloc_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // PAYMENTS TABLE
        // ================================================================
        migrationBuilder.CreateTable(
            name: "payments",
            schema: "wallets",
            columns: table => new
            {
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                intent_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_payments", x => x.payment_id);
                table.ForeignKey(
                    name: "fk_payment_intent",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // PAYMENT ALLOCATIONS TABLE
        // ================================================================
        migrationBuilder.CreateTable(
            name: "payment_allocations",
            schema: "wallets",
            columns: table => new
            {
                allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_payment_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "fk_payment_alloc_payment",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_payment_alloc_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_payment_alloc_ledger",
                    column: x => x.ledger_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // INDEXES
        // ================================================================
        migrationBuilder.CreateIndex(
            name: "ux_payment_intent_order_idem",
            schema: "wallets",
            table: "payment_intents",
            columns: new[] { "order_id", "idempotency_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_payment_intent_order",
            schema: "wallets",
            table: "payment_intents",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_payment_intent_alloc_intent",
            schema: "wallets",
            table: "payment_intent_allocations",
            column: "intent_id");

        migrationBuilder.CreateIndex(
            name: "idx_payment_intent_alloc_wallet",
            schema: "wallets",
            table: "payment_intent_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_payment_intent_alloc_intent_wallet",
            schema: "wallets",
            table: "payment_intent_allocations",
            columns: new[] { "intent_id", "wallet_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_payment_intent",
            schema: "wallets",
            table: "payments",
            column: "intent_id");

        migrationBuilder.CreateIndex(
            name: "idx_payment_order",
            schema: "wallets",
            table: "payments",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_payment_alloc_payment",
            schema: "wallets",
            table: "payment_allocations",
            column: "payment_id");

        migrationBuilder.CreateIndex(
            name: "idx_payment_alloc_wallet",
            schema: "wallets",
            table: "payment_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_payment_alloc_payment_wallet",
            schema: "wallets",
            table: "payment_allocations",
            columns: new[] { "payment_id", "wallet_id" },
            unique: true);

        // ================================================================
        // IDEMPOTENCY TABLES FOR intent operations
        // ================================================================
        migrationBuilder.CreateTable(
            name: "intent_operation_idempotency",
            schema: "wallets",
            columns: table => new
            {
                idempotency_id = table.Column<Guid>(type: "uuid", nullable: false),
                intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                idempotency_key = table.Column<string>(type: "text", nullable: false),
                operation_type = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                result_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_intent_operation_idempotency", x => x.idempotency_id);
                table.ForeignKey(
                    name: "fk_intent_idem_intent",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_intent_idem_intent_key_optype",
            schema: "wallets",
            table: "intent_operation_idempotency",
            columns: new[] { "intent_id", "idempotency_key", "operation_type" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "intent_operation_idempotency", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "payments", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_intent_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_intents", schema: "wallets");
    }
}
