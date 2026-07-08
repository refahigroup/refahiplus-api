using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations;

/// <summary>
/// Creates the Dapper-owned persistence tables used by the atomic payment flow.
/// These tables are intentionally not part of the EF model, but their schema is
/// still versioned through EF migrations so every environment receives it.
/// </summary>
public partial class WalletsAddPaymentFlowTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
                table.PrimaryKey("PK_payment_intents", x => x.intent_id);
            });

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
                table.PrimaryKey("PK_payment_intent_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "FK_payment_intent_allocations_intents",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_payment_intent_allocations_wallets",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
            });

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
                table.PrimaryKey("PK_intent_operation_idempotency", x => x.idempotency_id);
                table.ForeignKey(
                    name: "FK_intent_operation_idempotency_intents",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            schema: "wallets",
            columns: table => new
            {
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.payment_id);
                table.ForeignKey(
                    name: "FK_payments_payment_intents",
                    column: x => x.intent_id,
                    principalSchema: "wallets",
                    principalTable: "payment_intents",
                    principalColumn: "intent_id",
                    onDelete: ReferentialAction.Restrict);
            });

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
                table.PrimaryKey("PK_payment_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "FK_payment_allocations_payments",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_payment_allocations_wallets",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_payment_allocations_ledger_entries",
                    column: x => x.ledger_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "refunds",
            schema: "wallets",
            columns: table => new
            {
                refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                reason = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refunds", x => x.refund_id);
                table.ForeignKey(
                    name: "FK_refunds_payments",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "refund_operation_idempotency",
            schema: "wallets",
            columns: table => new
            {
                idempotency_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                idempotency_key = table.Column<string>(type: "text", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                result_refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refund_operation_idempotency", x => x.idempotency_id);
                table.ForeignKey(
                    name: "FK_refund_operation_idempotency_payments",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "refund_allocations",
            schema: "wallets",
            columns: table => new
            {
                allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount_minor = table.Column<long>(type: "bigint", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refund_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "FK_refund_allocations_refunds",
                    column: x => x.refund_id,
                    principalSchema: "wallets",
                    principalTable: "refunds",
                    principalColumn: "refund_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_refund_allocations_wallets",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_refund_allocations_ledger_entries",
                    column: x => x.ledger_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_payment_intents_order_idempotency",
            schema: "wallets",
            table: "payment_intents",
            columns: new[] { "order_id", "idempotency_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_payment_intent_allocations_intent_sequence",
            schema: "wallets",
            table: "payment_intent_allocations",
            columns: new[] { "intent_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_payment_intent_allocations_wallet",
            schema: "wallets",
            table: "payment_intent_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_intent_operation_idempotency_key",
            schema: "wallets",
            table: "intent_operation_idempotency",
            columns: new[] { "intent_id", "idempotency_key", "operation_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_payments_intent",
            schema: "wallets",
            table: "payments",
            column: "intent_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_payments_order",
            schema: "wallets",
            table: "payments",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ux_payment_allocations_payment_sequence",
            schema: "wallets",
            table: "payment_allocations",
            columns: new[] { "payment_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_payment_allocations_wallet",
            schema: "wallets",
            table: "payment_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_payment_allocations_ledger_entry",
            schema: "wallets",
            table: "payment_allocations",
            column: "ledger_entry_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_refunds_payment",
            schema: "wallets",
            table: "refunds",
            column: "payment_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_refunds_order",
            schema: "wallets",
            table: "refunds",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ux_refund_operation_idempotency_key",
            schema: "wallets",
            table: "refund_operation_idempotency",
            columns: new[] { "payment_id", "idempotency_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_refund_allocations_refund_sequence",
            schema: "wallets",
            table: "refund_allocations",
            columns: new[] { "refund_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_refund_allocations_wallet",
            schema: "wallets",
            table: "refund_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_refund_allocations_ledger_entry",
            schema: "wallets",
            table: "refund_allocations",
            column: "ledger_entry_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "refund_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "refund_operation_idempotency", schema: "wallets");
        migrationBuilder.DropTable(name: "refunds", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "intent_operation_idempotency", schema: "wallets");
        migrationBuilder.DropTable(name: "payments", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_intent_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "payment_intents", schema: "wallets");
    }
}
