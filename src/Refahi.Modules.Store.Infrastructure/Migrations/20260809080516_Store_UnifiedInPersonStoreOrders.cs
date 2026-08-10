using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_UnifiedInPersonStoreOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_store_order_items_OfferId",
                schema: "store",
                table: "store_order_items"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "store",
                table: "store_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<string>(
                name: "InitiatorType",
                schema: "store",
                table: "store_orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "User"
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OtpExpiresAt",
                schema: "store",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "OtpReferenceCode",
                schema: "store",
                table: "store_orders",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OtpVerifiedAt",
                schema: "store",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "OfferId",
                schema: "store",
                table: "store_order_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<long>(
                name: "DeclaredGrossAmountMinor",
                schema: "store",
                table: "store_order_items",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE store.store_orders
                SET "CreatedByUserId" = "UserId"
                WHERE "CreatedByUserId" = '00000000-0000-0000-0000-000000000000';
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_SalesChannel_ShopId_CreatedAt",
                schema: "store",
                table: "store_orders",
                columns: new[] { "SalesChannel", "ShopId", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_OfferId",
                schema: "store",
                table: "store_order_items",
                column: "OfferId",
                filter: "\"OfferId\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_store_orders_SalesChannel_ShopId_CreatedAt",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropIndex(
                name: "IX_store_order_items_OfferId",
                schema: "store",
                table: "store_order_items"
            );

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropColumn(
                name: "InitiatorType",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropColumn(
                name: "OtpExpiresAt",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropColumn(
                name: "OtpReferenceCode",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropColumn(
                name: "OtpVerifiedAt",
                schema: "store",
                table: "store_orders"
            );

            migrationBuilder.DropColumn(
                name: "DeclaredGrossAmountMinor",
                schema: "store",
                table: "store_order_items"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "OfferId",
                schema: "store",
                table: "store_order_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_OfferId",
                schema: "store",
                table: "store_order_items",
                column: "OfferId"
            );
        }
    }
}
