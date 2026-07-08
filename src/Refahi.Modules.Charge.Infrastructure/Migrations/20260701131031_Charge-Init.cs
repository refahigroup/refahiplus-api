using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Charge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChargeInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "charge");

            migrationBuilder.CreateTable(
                name: "charge_markup_rules",
                schema: "charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    @operator = table.Column<short>(name: "operator", type: "smallint", nullable: true),
                    service_type = table.Column<short>(type: "smallint", nullable: true),
                    percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    fixed_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_markup_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_requests",
                schema: "charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saga_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    @operator = table.Column<short>(name: "operator", type: "smallint", nullable: false),
                    service_type = table.Column<short>(type: "smallint", nullable: false),
                    destination_mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    provider_product_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    product_caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    product_category = table.Column<int>(type: "integer", nullable: false),
                    pay_bill = table.Column<int>(type: "integer", nullable: false),
                    pin_category_id = table.Column<int>(type: "integer", nullable: true),
                    pin_count = table.Column<int>(type: "integer", nullable: false),
                    product_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    provider_cost_minor = table.Column<long>(type: "bigint", nullable: false),
                    markup_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    markup_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    markup_fixed_minor = table.Column<long>(type: "bigint", nullable: false),
                    markup_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    final_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_invoice_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_rrn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_trace_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    eniac_result_code = table.Column<int>(type: "integer", nullable: true),
                    operator_result_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    provider_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expire_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fulfilled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_reconciliation_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reconciliation_count = table.Column<int>(type: "integer", nullable: false),
                    processing_lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_fulfillment_attempts",
                schema: "charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    eniac_result_code = table.Column<int>(type: "integer", nullable: true),
                    operator_result_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    provider_rrn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_trace_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    request_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    latency_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_fulfillment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_charge_fulfillment_attempts_charge_requests_charge_request_~",
                        column: x => x.charge_request_id,
                        principalSchema: "charge",
                        principalTable: "charge_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charge_pins",
                schema: "charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_serial = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    encrypted_code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_pins", x => x.id);
                    table.ForeignKey(
                        name: "FK_charge_pins_charge_requests_charge_request_id",
                        column: x => x.charge_request_id,
                        principalSchema: "charge",
                        principalTable: "charge_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_charge_fulfillment_attempts_charge_request_id_created_at",
                schema: "charge",
                table: "charge_fulfillment_attempts",
                columns: new[] { "charge_request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_markup_scope",
                schema: "charge",
                table: "charge_markup_rules",
                columns: new[] { "is_active", "operator", "service_type", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_charge_pins_charge_request_id",
                schema: "charge",
                table: "charge_pins",
                column: "charge_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_charge_requests_work",
                schema: "charge",
                table: "charge_requests",
                columns: new[] { "status", "next_reconciliation_at" });

            migrationBuilder.CreateIndex(
                name: "ux_charge_requests_invoice",
                schema: "charge",
                table: "charge_requests",
                column: "customer_invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_charge_requests_order_id",
                schema: "charge",
                table: "charge_requests",
                column: "order_id",
                unique: true,
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_charge_requests_user_idempotency",
                schema: "charge",
                table: "charge_requests",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_fulfillment_attempts",
                schema: "charge");

            migrationBuilder.DropTable(
                name: "charge_markup_rules",
                schema: "charge");

            migrationBuilder.DropTable(
                name: "charge_pins",
                schema: "charge");

            migrationBuilder.DropTable(
                name: "charge_requests",
                schema: "charge");
        }
    }
}
