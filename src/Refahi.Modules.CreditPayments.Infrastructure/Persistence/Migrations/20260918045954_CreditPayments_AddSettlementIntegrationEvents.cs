using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddSettlementIntegrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_events",
                schema: "credit_payments",
                columns: table => new
                {
                    integration_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_version = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_events", x => x.integration_event_id);
                    table.CheckConstraint("ck_integration_events_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_integration_events_payload_size", "octet_length(payload_json::text) <= 32768");
                    table.CheckConstraint("ck_integration_events_published", "(status = 3 AND published_at IS NOT NULL AND lease_owner IS NULL AND lease_until IS NULL) OR (status <> 3 AND published_at IS NULL)");
                    table.CheckConstraint("ck_integration_events_retry", "retry_count >= 0");
                    table.CheckConstraint("ck_integration_events_source", "source = 'CreditPayments'");
                    table.CheckConstraint("ck_integration_events_status", "status IN (1,2,3)");
                    table.CheckConstraint("ck_integration_events_type", "event_type IN ('credit-payment-captured.v1','credit-payment-refunded.v1')");
                    table.CheckConstraint("ck_integration_events_version", "event_version > 0");
                    table.CheckConstraint("ck_integration_events_version_token", "version > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_events_aggregate",
                schema: "credit_payments",
                table: "integration_events",
                columns: new[] { "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_events_publish_due",
                schema: "credit_payments",
                table: "integration_events",
                columns: new[] { "status", "next_retry_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_events_type_occurred",
                schema: "credit_payments",
                table: "integration_events",
                columns: new[] { "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_integration_events_source_key",
                schema: "credit_payments",
                table: "integration_events",
                column: "source_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_events",
                schema: "credit_payments");
        }
    }
}
