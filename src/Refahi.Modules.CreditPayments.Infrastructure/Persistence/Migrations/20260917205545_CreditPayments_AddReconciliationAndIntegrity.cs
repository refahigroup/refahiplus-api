using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddReconciliationAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                schema: "credit_payments",
                columns: table => new
                {
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    worker_instance_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    range_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    range_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    scanned_count = table.Column<int>(type: "integer", nullable: false),
                    finding_count = table.Column<int>(type: "integer", nullable: false),
                    auto_remediation_count = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    cursor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.reconciliation_run_id);
                    table.CheckConstraint("ck_reconciliation_runs_completion", "(status = 1 AND completed_at IS NULL) OR (status <> 1 AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_reconciliation_runs_counts", "scanned_count >= 0 AND finding_count >= 0 AND auto_remediation_count >= 0 AND error_count >= 0");
                    table.CheckConstraint("ck_reconciliation_runs_kind", "kind BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_reconciliation_runs_lease", "lease_until > started_at");
                    table.CheckConstraint("ck_reconciliation_runs_range", "range_from IS NULL OR range_to IS NULL OR range_from <= range_to");
                    table.CheckConstraint("ck_reconciliation_runs_status", "status BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_reconciliation_runs_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_findings",
                schema: "credit_payments",
                columns: table => new
                {
                    reconciliation_finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    finding_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<short>(type: "smallint", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    first_observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observation_count = table.Column<int>(type: "integer", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_category = table.Column<short>(type: "smallint", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acknowledgement_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auto_remediation_action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    auto_remediation_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_auto_remediation_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_auto_remediation_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_json = table.Column<string>(type: "jsonb", maxLength: 8192, nullable: false),
                    evidence_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_findings", x => x.reconciliation_finding_id);
                    table.CheckConstraint("ck_reconciliation_findings_acknowledged", "status <> 2 OR (acknowledged_at IS NOT NULL AND acknowledged_by IS NOT NULL)");
                    table.CheckConstraint("ck_reconciliation_findings_kind", "kind BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_reconciliation_findings_observation", "observation_count > 0 AND first_observed_at <= last_observed_at");
                    table.CheckConstraint("ck_reconciliation_findings_remediation", "auto_remediation_attempt_count >= 0");
                    table.CheckConstraint("ck_reconciliation_findings_resolution", "resolution_category BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_reconciliation_findings_resolved", "(status = 3 AND resolved_at IS NOT NULL) OR (status <> 3 AND resolved_at IS NULL)");
                    table.CheckConstraint("ck_reconciliation_findings_severity", "severity BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_reconciliation_findings_status", "status BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_reconciliation_findings_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_reconciliation_findings_reconciliation_runs_reconciliation_~",
                        column: x => x.reconciliation_run_id,
                        principalSchema: "credit_payments",
                        principalTable: "reconciliation_runs",
                        principalColumn: "reconciliation_run_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_findings_open_critical",
                schema: "credit_payments",
                table: "reconciliation_findings",
                columns: new[] { "severity", "first_observed_at" },
                filter: "status <> 3 AND severity = 3");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_findings_reconciliation_run_id",
                schema: "credit_payments",
                table: "reconciliation_findings",
                column: "reconciliation_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_findings_resource",
                schema: "credit_payments",
                table: "reconciliation_findings",
                columns: new[] { "resource_type", "resource_id", "last_observed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_findings_status_severity_date",
                schema: "credit_payments",
                table: "reconciliation_findings",
                columns: new[] { "status", "severity", "last_observed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_findings_active_logical",
                schema: "credit_payments",
                table: "reconciliation_findings",
                columns: new[] { "finding_code", "resource_type", "resource_id" },
                unique: true,
                filter: "status <> 3");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_runs_kind_started",
                schema: "credit_payments",
                table: "reconciliation_runs",
                columns: new[] { "kind", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_runs_active_kind",
                schema: "credit_payments",
                table: "reconciliation_runs",
                column: "kind",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconciliation_findings",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "reconciliation_runs",
                schema: "credit_payments");
        }
    }
}
