using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddOnlineCartAndStoreOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OfferId",
                schema: "store",
                table: "cart_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OriginalUnitPriceMinor",
                schema: "store",
                table: "cart_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "store_orders",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannel = table.Column<short>(type: "smallint", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    DiscountAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    FinalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShippingAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryTimeSlot = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.Id);
                    table.CheckConstraint("CK_store_orders_amounts", "\"OriginalAmountMinor\" >= \"FinalAmountMinor\" AND \"FinalAmountMinor\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "store_order_items",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCartItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VariantTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SessionTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannel = table.Column<short>(type: "smallint", nullable: false),
                    ProductType = table.Column<short>(type: "smallint", nullable: false),
                    SalesModel = table.Column<short>(type: "smallint", nullable: false),
                    FulfillmentMethod = table.Column<short>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OriginalUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementCategoryTermId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CommissionAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryMethod = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_order_items_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalSchema: "store",
                        principalTable: "store_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_OfferId",
                schema: "store",
                table: "cart_items",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_OfferId",
                schema: "store",
                table: "store_order_items",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_StoreOrderId",
                schema: "store",
                table: "store_order_items",
                column: "StoreOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_OrderId",
                schema: "store",
                table: "store_orders",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_UserId_IdempotencyKey",
                schema: "store",
                table: "store_orders",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_order_items",
                schema: "store");

            migrationBuilder.DropTable(
                name: "store_orders",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_OfferId",
                schema: "store",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "OfferId",
                schema: "store",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "OriginalUnitPriceMinor",
                schema: "store",
                table: "cart_items");
        }
    }
}
