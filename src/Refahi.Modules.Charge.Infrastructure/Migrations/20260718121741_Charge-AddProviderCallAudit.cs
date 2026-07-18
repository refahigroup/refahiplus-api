using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Charge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChargeAddProviderCallAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "provider_call_log_id",
                schema: "charge",
                table: "charge_fulfillment_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "provider_call_logs",
                schema: "charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    saga_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    outcome = table.Column<short>(type: "smallint", nullable: false),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    provider_result_code = table.Column<int>(type: "integer", nullable: true),
                    operator_result_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retryable = table.Column<bool>(type: "boolean", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    exception_type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    request_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    latency_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_call_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_charge_fulfillment_attempts_provider_call_log_id",
                schema: "charge",
                table: "charge_fulfillment_attempts",
                column: "provider_call_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_charge_request_id_created_at",
                schema: "charge",
                table: "provider_call_logs",
                columns: new[] { "charge_request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_correlation_id",
                schema: "charge",
                table: "provider_call_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_outcome_created_at",
                schema: "charge",
                table: "provider_call_logs",
                columns: new[] { "outcome", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_provider_name_operation_created_at",
                schema: "charge",
                table: "provider_call_logs",
                columns: new[] { "provider_name", "operation", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_charge_fulfillment_attempts_provider_call_logs_provider_cal~",
                schema: "charge",
                table: "charge_fulfillment_attempts",
                column: "provider_call_log_id",
                principalSchema: "charge",
                principalTable: "provider_call_logs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_charge_fulfillment_attempts_provider_call_logs_provider_cal~",
                schema: "charge",
                table: "charge_fulfillment_attempts");

            migrationBuilder.DropTable(
                name: "provider_call_logs",
                schema: "charge");

            migrationBuilder.DropIndex(
                name: "IX_charge_fulfillment_attempts_provider_call_log_id",
                schema: "charge",
                table: "charge_fulfillment_attempts");

            migrationBuilder.DropColumn(
                name: "provider_call_log_id",
                schema: "charge",
                table: "charge_fulfillment_attempts");
        }
    }
}
