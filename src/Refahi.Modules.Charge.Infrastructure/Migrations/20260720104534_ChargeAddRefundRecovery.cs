using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Charge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChargeAddRefundRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "refund_attempt_count",
                schema: "charge",
                table: "charge_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "refund_idempotency_key",
                schema: "charge",
                table: "charge_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refund_last_attempt_at",
                schema: "charge",
                table: "charge_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_last_error",
                schema: "charge",
                table: "charge_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_reason",
                schema: "charge",
                table: "charge_requests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refund_started_at",
                schema: "charge",
                table: "charge_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refund_attempt_count",
                schema: "charge",
                table: "charge_requests");

            migrationBuilder.DropColumn(
                name: "refund_idempotency_key",
                schema: "charge",
                table: "charge_requests");

            migrationBuilder.DropColumn(
                name: "refund_last_attempt_at",
                schema: "charge",
                table: "charge_requests");

            migrationBuilder.DropColumn(
                name: "refund_last_error",
                schema: "charge",
                table: "charge_requests");

            migrationBuilder.DropColumn(
                name: "refund_reason",
                schema: "charge",
                table: "charge_requests");

            migrationBuilder.DropColumn(
                name: "refund_started_at",
                schema: "charge",
                table: "charge_requests");
        }
    }
}
