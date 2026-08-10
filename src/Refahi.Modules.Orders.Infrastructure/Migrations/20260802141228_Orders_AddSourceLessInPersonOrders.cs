using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Orders_AddSourceLessInPersonOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "source_reference_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "source_item_id",
                schema: "orders",
                table: "order_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_by_created_at",
                schema: "orders",
                table: "orders",
                columns: new[] { "created_by_user_id", "created_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_created_by_created_at",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "source_reference_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "source_item_id",
                schema: "orders",
                table: "order_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );
        }
    }
}
