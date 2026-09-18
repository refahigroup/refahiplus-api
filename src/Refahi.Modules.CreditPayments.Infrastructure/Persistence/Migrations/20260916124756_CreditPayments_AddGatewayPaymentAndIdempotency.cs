using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddGatewayPaymentAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                schema: "credit_payments",
                table: "audit_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant_order_id",
                schema: "credit_payments",
                table: "audit_records",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "terminal_id",
                schema: "credit_payments",
                table: "audit_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gateway_payments",
                schema: "credit_payments",
                columns: table => new
                {
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    return_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    merchant_display_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    terminal_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    approved_category_code_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_payments", x => x.gateway_payment_id);
                    table.CheckConstraint("ck_gateway_payments_amount", "amount_minor > 0");
                    table.CheckConstraint("ck_gateway_payments_currency", "currency = 'IRR'");
                    table.CheckConstraint("ck_gateway_payments_description_length", "description IS NULL OR char_length(description) <= 250");
                    table.CheckConstraint("ck_gateway_payments_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_gateway_payments_metadata_length", "octet_length(metadata::text) <= 4096");
                    table.CheckConstraint("ck_gateway_payments_order_length", "char_length(merchant_order_id) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_gateway_payments_return_url_length", "char_length(return_url) BETWEEN 1 AND 2048");
                    table.CheckConstraint("ck_gateway_payments_status", "status IN (1,2,3,4,5,6,7,8,9,10)");
                    table.CheckConstraint("ck_gateway_payments_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_gateway_payments_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "credit_payments",
                        principalTable: "merchants",
                        principalColumn: "merchant_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gateway_payments_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "credit_payments",
                columns: table => new
                {
                    idempotency_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    request_hash_version = table.Column<int>(type: "integer", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.idempotency_record_id);
                    table.CheckConstraint("ck_idempotency_hash", "octet_length(request_hash) = 32");
                    table.CheckConstraint("ck_idempotency_hash_version", "request_hash_version = 1");
                    table.CheckConstraint("ck_idempotency_key_length", "char_length(idempotency_key) BETWEEN 8 AND 128");
                    table.CheckConstraint("ck_idempotency_resource_state", "(state = 1 AND resource_id IS NULL AND completed_at IS NULL) OR (state = 2 AND resource_id IS NOT NULL AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_idempotency_state", "state IN (1,2)");
                    table.ForeignKey(
                        name: "FK_idempotency_records_gateway_payments_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_idempotency_records_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payments_merchant_id",
                schema: "credit_payments",
                table: "gateway_payments",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ux_gateway_payments_terminal_order",
                schema: "credit_payments",
                table: "gateway_payments",
                columns: new[] { "terminal_id", "merchant_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_resource_id",
                schema: "credit_payments",
                table: "idempotency_records",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_terminal_operation_key",
                schema: "credit_payments",
                table: "idempotency_records",
                columns: new[] { "terminal_id", "operation_type", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "gateway_payments",
                schema: "credit_payments");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "credit_payments",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "merchant_order_id",
                schema: "credit_payments",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "terminal_id",
                schema: "credit_payments",
                table: "audit_records");
        }
    }
}
