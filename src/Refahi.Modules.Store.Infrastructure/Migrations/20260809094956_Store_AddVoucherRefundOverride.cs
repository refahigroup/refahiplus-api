using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddVoucherRefundOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voucher_refund_overrides",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VoucherSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_refund_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_refund_overrides_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalSchema: "store",
                        principalTable: "store_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_refund_override_attempts",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherRefundOverrideId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaymentAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_refund_override_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_refund_override_attempts_voucher_refund_overrides_V~",
                        column: x => x.VoucherRefundOverrideId,
                        principalSchema: "store",
                        principalTable: "voucher_refund_overrides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_refund_override_attempts_VoucherRefundOverrideId_Se~",
                schema: "store",
                table: "voucher_refund_override_attempts",
                columns: new[] { "VoucherRefundOverrideId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_refund_overrides_CorrelationId",
                schema: "store",
                table: "voucher_refund_overrides",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_refund_overrides_IdempotencyKey",
                schema: "store",
                table: "voucher_refund_overrides",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_refund_overrides_OrderId",
                schema: "store",
                table: "voucher_refund_overrides",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_refund_overrides_StoreOrderId",
                schema: "store",
                table: "voucher_refund_overrides",
                column: "StoreOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_refund_override_attempts",
                schema: "store");

            migrationBuilder.DropTable(
                name: "voucher_refund_overrides",
                schema: "store");
        }
    }
}
