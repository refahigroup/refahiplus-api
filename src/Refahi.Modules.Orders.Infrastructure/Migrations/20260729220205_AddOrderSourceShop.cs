using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSourceShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_shop_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE orders.orders
                SET source_shop_id = source_reference_id
                WHERE lower(source_module) = 'store'
                  AND reference_type <> 'StoreInPerson'
                  AND source_shop_id IS NULL;
                """
            );

            migrationBuilder.CreateIndex(
                name: "ix_orders_source_shop_created_at",
                schema: "orders",
                table: "orders",
                columns: new[] { "source_module", "source_shop_id", "created_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_source_shop_created_at",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(name: "source_shop_id", schema: "orders", table: "orders");
        }
    }
}
