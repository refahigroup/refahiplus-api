using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_idempotency_records_gateway_payments_resource_id",
                schema: "credit_payments",
                table: "idempotency_records");

            migrationBuilder.DropIndex(
                name: "IX_idempotency_records_resource_id",
                schema: "credit_payments",
                table: "idempotency_records");

            migrationBuilder.AddColumn<long>(
                name: "refunded_amount_minor",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "credit_payments",
                columns: table => new
                {
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    merchant_refund_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    normalized_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    wallet_refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    order_projection_status = table.Column<short>(type: "smallint", nullable: false),
                    order_projection_retry_count = table.Column<int>(type: "integer", nullable: false),
                    order_projection_next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    order_refund_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.refund_id);
                    table.CheckConstraint("ck_refunds_amount", "amount_minor > 0");
                    table.CheckConstraint("ck_refunds_completion", "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5) AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_refunds_currency", "currency = 'IRR'");
                    table.CheckConstraint("ck_refunds_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_refunds_projection_status", "order_projection_status IN (1,2,3)");
                    table.CheckConstraint("ck_refunds_retry", "retry_count >= 0 AND order_projection_retry_count >= 0");
                    table.CheckConstraint("ck_refunds_status", "status IN (1,2,3,4,5)");
                    table.CheckConstraint("ck_refunds_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_refunds_gateway_payments_gateway_payment_id",
                        column: x => x.gateway_payment_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_refund_state",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "(status <> 9 OR (refunded_amount_minor > 0 AND refunded_amount_minor < captured_amount_minor)) AND (status <> 10 OR (captured_amount_minor > 0 AND refunded_amount_minor = captured_amount_minor))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_refunded_amount",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "refunded_amount_minor >= 0 AND refunded_amount_minor <= captured_amount_minor");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_gateway_payment",
                schema: "credit_payments",
                table: "refunds",
                column: "gateway_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_recovery",
                schema: "credit_payments",
                table: "refunds",
                columns: new[] { "status", "next_retry_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_refunds_terminal_merchant_reference",
                schema: "credit_payments",
                table: "refunds",
                columns: new[] { "terminal_id", "merchant_refund_reference" },
                unique: true,
                filter: "\"merchant_refund_reference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_refunds_wallet_refund",
                schema: "credit_payments",
                table: "refunds",
                column: "wallet_refund_id",
                unique: true,
                filter: "\"wallet_refund_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refunds",
                schema: "credit_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_refund_state",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_refunded_amount",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "refunded_amount_minor",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_resource_id",
                schema: "credit_payments",
                table: "idempotency_records",
                column: "resource_id");

            migrationBuilder.AddForeignKey(
                name: "FK_idempotency_records_gateway_payments_resource_id",
                schema: "credit_payments",
                table: "idempotency_records",
                column: "resource_id",
                principalSchema: "credit_payments",
                principalTable: "gateway_payments",
                principalColumn: "gateway_payment_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
