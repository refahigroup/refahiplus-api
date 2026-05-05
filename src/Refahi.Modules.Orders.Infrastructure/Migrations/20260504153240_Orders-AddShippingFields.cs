using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrdersAddShippingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "delivery_date",
                schema: "orders",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "delivery_time_slot",
                schema: "orders",
                table: "orders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "discount_code",
                schema: "orders",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "discount_code_amount_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "shipping_address_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipping_address_snapshot",
                schema: "orders",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "shipping_fee_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<short>(
                name: "delivery_method",
                schema: "orders",
                table: "order_items",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delivery_date",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_time_slot",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_code",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_code_amount_minor",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_address_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_address_snapshot",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_fee_minor",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_method",
                schema: "orders",
                table: "order_items");
        }
    }
}
