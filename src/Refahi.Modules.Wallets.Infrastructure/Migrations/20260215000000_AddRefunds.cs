using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations;

/// <summary>
/// Sprint-06: Add Refund tables and idempotency tracking.
/// Refund is allocation-aware - returns money to same wallets that paid, using same allocations.
/// </summary>
public partial class AddRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ================================================================
        // REFUNDS TABLE
        // ================================================================
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
                table.PrimaryKey("pk_refunds", x => x.refund_id);
                table.ForeignKey(
                    name: "fk_refund_payment",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // REFUND ALLOCATIONS TABLE
        // ================================================================
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
                table.PrimaryKey("pk_refund_allocations", x => x.allocation_id);
                table.ForeignKey(
                    name: "fk_refund_alloc_refund",
                    column: x => x.refund_id,
                    principalSchema: "wallets",
                    principalTable: "refunds",
                    principalColumn: "refund_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_refund_alloc_wallet",
                    column: x => x.wallet_id,
                    principalSchema: "wallets",
                    principalTable: "wallets",
                    principalColumn: "wallet_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_refund_alloc_ledger",
                    column: x => x.ledger_entry_id,
                    principalSchema: "wallets",
                    principalTable: "ledger_entries",
                    principalColumn: "ledger_entry_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // REFUND OPERATION IDEMPOTENCY TABLE
        // ================================================================
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
                table.PrimaryKey("pk_refund_operation_idempotency", x => x.idempotency_id);
                table.ForeignKey(
                    name: "fk_refund_idem_payment",
                    column: x => x.payment_id,
                    principalSchema: "wallets",
                    principalTable: "payments",
                    principalColumn: "payment_id",
                    onDelete: ReferentialAction.Restrict);
            });

        // ================================================================
        // INDEXES
        // ================================================================
        // Only one refund allowed per payment (full refund only)
        migrationBuilder.CreateIndex(
            name: "ux_refund_payment",
            schema: "wallets",
            table: "refunds",
            column: "payment_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_refund_order",
            schema: "wallets",
            table: "refunds",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_refund_alloc_refund",
            schema: "wallets",
            table: "refund_allocations",
            column: "refund_id");

        migrationBuilder.CreateIndex(
            name: "idx_refund_alloc_wallet",
            schema: "wallets",
            table: "refund_allocations",
            column: "wallet_id");

        migrationBuilder.CreateIndex(
            name: "ux_refund_alloc_refund_wallet",
            schema: "wallets",
            table: "refund_allocations",
            columns: new[] { "refund_id", "wallet_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_refund_idem_payment_key",
            schema: "wallets",
            table: "refund_operation_idempotency",
            columns: new[] { "payment_id", "idempotency_key" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "refund_operation_idempotency", schema: "wallets");
        migrationBuilder.DropTable(name: "refund_allocations", schema: "wallets");
        migrationBuilder.DropTable(name: "refunds", schema: "wallets");
    }
}
