using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddPaymentExecutionAndAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "captured_amount_minor",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_hash",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_id",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_version",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "wallet_payment_id",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                schema: "credit_payments",
                columns: table => new
                {
                    payment_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_sequence = table.Column<int>(type: "integer", nullable: false),
                    trigger = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    normalized_outcome_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    destination_wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_intent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wallet_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.payment_attempt_id);
                    table.CheckConstraint("ck_payment_attempts_completion", "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5) AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_payment_attempts_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_payment_attempts_retry", "retry_count >= 0");
                    table.CheckConstraint("ck_payment_attempts_sequence", "attempt_sequence > 0");
                    table.CheckConstraint("ck_payment_attempts_status", "status IN (1,2,3,4,5)");
                    table.CheckConstraint("ck_payment_attempts_trigger", "trigger IN (1,2,3)");
                    table.CheckConstraint("ck_payment_attempts_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_payment_attempts_gateway_payments_gateway_payment_id",
                        column: x => x.gateway_payment_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_gateway_payments_wallet_payment_id",
                schema: "credit_payments",
                table: "gateway_payments",
                column: "wallet_payment_id",
                unique: true,
                filter: "\"wallet_payment_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_captured_amount",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "captured_amount_minor >= 0 AND captured_amount_minor <= amount_minor");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_policy",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "(policy_id IS NULL AND policy_version IS NULL AND policy_hash IS NULL) OR (policy_id IS NOT NULL AND policy_version IS NOT NULL AND policy_hash IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_success",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "status <> 5 OR (captured_amount_minor = amount_minor AND paid_at IS NOT NULL AND wallet_payment_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_recovery",
                schema: "credit_payments",
                table: "payment_attempts",
                columns: new[] { "status", "next_retry_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_attempts_active_payment",
                schema: "credit_payments",
                table: "payment_attempts",
                column: "gateway_payment_id",
                unique: true,
                filter: "\"status\" IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "ux_payment_attempts_payment_sequence",
                schema: "credit_payments",
                table: "payment_attempts",
                columns: new[] { "gateway_payment_id", "attempt_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_attempts_wallet_payment_id",
                schema: "credit_payments",
                table: "payment_attempts",
                column: "wallet_payment_id",
                unique: true,
                filter: "\"wallet_payment_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_attempts",
                schema: "credit_payments");

            migrationBuilder.DropIndex(
                name: "ux_gateway_payments_wallet_payment_id",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_captured_amount",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_policy",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_success",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "captured_amount_minor",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "paid_at",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "policy_hash",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "policy_id",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "policy_version",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "wallet_payment_id",
                schema: "credit_payments",
                table: "gateway_payments");
        }
    }
}
