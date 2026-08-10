using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddVoucherLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vouchers",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedShopId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedShopName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.Id);
                    table.CheckConstraint("CK_vouchers_sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_vouchers_store_order_items_StoreOrderItemId",
                        column: x => x.StoreOrderItemId,
                        principalSchema: "store",
                        principalTable: "store_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vouchers_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalSchema: "store",
                        principalTable: "store_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalSchema: "store",
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_SupplierId_ShopId_RedeemedAtUtc",
                schema: "store",
                table: "voucher_redemptions",
                columns: new[] { "SupplierId", "ShopId", "RedeemedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_VendorUserId_IdempotencyKey",
                schema: "store",
                table: "voucher_redemptions",
                columns: new[] { "VendorUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_VoucherId",
                schema: "store",
                table: "voucher_redemptions",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_CodeHash",
                schema: "store",
                table: "vouchers",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_OrderId",
                schema: "store",
                table: "vouchers",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_StoreOrderId_Status",
                schema: "store",
                table: "vouchers",
                columns: new[] { "StoreOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_StoreOrderItemId_SequenceNumber",
                schema: "store",
                table: "vouchers",
                columns: new[] { "StoreOrderItemId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_UserId_IssuedAtUtc",
                schema: "store",
                table: "vouchers",
                columns: new[] { "UserId", "IssuedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_redemptions",
                schema: "store");

            migrationBuilder.DropTable(
                name: "vouchers",
                schema: "store");
        }
    }
}
