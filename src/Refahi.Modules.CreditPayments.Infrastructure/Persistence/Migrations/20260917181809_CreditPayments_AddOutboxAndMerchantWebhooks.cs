using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddOutboxAndMerchantWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_events",
                schema: "credit_payments",
                columns: table => new
                {
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_version = table.Column<int>(type: "integer", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.webhook_event_id);
                    table.CheckConstraint("ck_webhook_events_payload_size", "octet_length(payload_json::text) <= 32768");
                    table.CheckConstraint("ck_webhook_events_type", "event_type IN ('payment.succeeded','payment.failed','payment.canceled','payment.expired','refund.succeeded','refund.failed')");
                    table.CheckConstraint("ck_webhook_events_version", "event_version > 0");
                    table.ForeignKey(
                        name: "FK_webhook_events_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "credit_payments",
                        principalTable: "merchants",
                        principalColumn: "merchant_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_events_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "credit_payments",
                columns: table => new
                {
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.outbox_message_id);
                    table.CheckConstraint("ck_outbox_messages_completion", "(status = 3 AND completed_at IS NOT NULL AND lease_owner IS NULL AND lease_until IS NULL) OR (status <> 3 AND completed_at IS NULL)");
                    table.CheckConstraint("ck_outbox_messages_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_outbox_messages_retry", "retry_count >= 0");
                    table.CheckConstraint("ck_outbox_messages_status", "status IN (1,2,3)");
                    table.CheckConstraint("ck_outbox_messages_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_outbox_messages_webhook_events_webhook_event_id",
                        column: x => x.webhook_event_id,
                        principalSchema: "credit_payments",
                        principalTable: "webhook_events",
                        principalColumn: "webhook_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                schema: "credit_payments",
                columns: table => new
                {
                    webhook_delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    manual_resend_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_http_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_secret_version_used = table.Column<int>(type: "integer", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.webhook_delivery_id);
                    table.CheckConstraint("ck_webhook_deliveries_attempts", "attempt_count >= 0 AND manual_resend_count >= 0");
                    table.CheckConstraint("ck_webhook_deliveries_delivered", "status <> 4 OR delivered_at IS NOT NULL");
                    table.CheckConstraint("ck_webhook_deliveries_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_webhook_deliveries_status", "status IN (1,2,3,4,5)");
                    table.CheckConstraint("ck_webhook_deliveries_terminal_lease", "status NOT IN (4,5) OR (lease_owner IS NULL AND lease_until IS NULL)");
                    table.CheckConstraint("ck_webhook_deliveries_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhook_events_webhook_event_id",
                        column: x => x.webhook_event_id,
                        principalSchema: "credit_payments",
                        principalTable: "webhook_events",
                        principalColumn: "webhook_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_dispatch",
                schema: "credit_payments",
                table: "outbox_messages",
                columns: new[] { "status", "next_retry_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_outbox_messages_webhook_event",
                schema: "credit_payments",
                table: "outbox_messages",
                column: "webhook_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_due",
                schema: "credit_payments",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_attempt_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_webhook_deliveries_webhook_event",
                schema: "credit_payments",
                table: "webhook_deliveries",
                column: "webhook_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_aggregate",
                schema: "credit_payments",
                table: "webhook_events",
                columns: new[] { "aggregate_type", "aggregate_id", "event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_merchant_id",
                schema: "credit_payments",
                table: "webhook_events",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_terminal_id",
                schema: "credit_payments",
                table: "webhook_events",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "ux_webhook_events_source_key",
                schema: "credit_payments",
                table: "webhook_events",
                column: "source_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "webhook_deliveries",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "webhook_events",
                schema: "credit_payments");
        }
    }
}
