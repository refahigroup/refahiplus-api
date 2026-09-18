using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddRiskAndOperationalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risk_decisions",
                schema: "credit_payments",
                columns: table => new
                {
                    risk_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decision = table.Column<short>(type: "smallint", nullable: false),
                    current_value = table.Column<long>(type: "bigint", nullable: true),
                    limit_value = table.Column<long>(type: "bigint", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_decisions", x => x.risk_decision_id);
                    table.CheckConstraint("ck_risk_decisions_decision", "decision IN (1,2)");
                    table.CheckConstraint("ck_risk_decisions_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
                    table.CheckConstraint("ck_risk_decisions_values", "(current_value IS NULL OR current_value >= 0) AND (limit_value IS NULL OR limit_value > 0)");
                });

            migrationBuilder.CreateTable(
                name: "risk_velocity_counters",
                schema: "credit_payments",
                columns: table => new
                {
                    risk_velocity_counter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<short>(type: "smallint", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bucket_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_count = table.Column<long>(type: "bigint", nullable: false),
                    accepted_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_velocity_counters", x => x.risk_velocity_counter_id);
                    table.CheckConstraint("ck_risk_counters_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
                    table.CheckConstraint("ck_risk_counters_scope", "scope_type IN (1,2)");
                    table.CheckConstraint("ck_risk_counters_values", "accepted_count >= 0 AND accepted_amount_minor >= 0");
                    table.CheckConstraint("ck_risk_counters_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "risk_reservations",
                schema: "credit_payments",
                columns: table => new
                {
                    risk_reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_velocity_counter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<short>(type: "smallint", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    bucket_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_reservations", x => x.risk_reservation_id);
                    table.CheckConstraint("ck_risk_reservations_amount", "amount_minor >= 0");
                    table.CheckConstraint("ck_risk_reservations_operation", "operation_type IN ('payment_create','payment_execution','refund_create','funding_start')");
                    table.ForeignKey(
                        name: "FK_risk_reservations_risk_velocity_counters_risk_velocity_coun~",
                        column: x => x.risk_velocity_counter_id,
                        principalSchema: "credit_payments",
                        principalTable: "risk_velocity_counters",
                        principalColumn: "risk_velocity_counter_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_risk_decisions_resource_date",
                schema: "credit_payments",
                table: "risk_decisions",
                columns: new[] { "resource_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_decisions_rule_date",
                schema: "credit_payments",
                table: "risk_decisions",
                columns: new[] { "rule_code", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_reservations_risk_velocity_counter_id",
                schema: "credit_payments",
                table: "risk_reservations",
                column: "risk_velocity_counter_id");

            migrationBuilder.CreateIndex(
                name: "ux_risk_reservations_resource",
                schema: "credit_payments",
                table: "risk_reservations",
                columns: new[] { "scope_type", "scope_id", "operation_type", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_risk_counters_scope_operation_bucket",
                schema: "credit_payments",
                table: "risk_velocity_counters",
                columns: new[] { "scope_type", "scope_id", "operation_type", "bucket_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "risk_decisions",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "risk_reservations",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "risk_velocity_counters",
                schema: "credit_payments");
        }
    }
}
