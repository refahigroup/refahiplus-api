using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSourceOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_owner_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE orders.orders AS o
                SET source_owner_id = s."SupplierId"
                FROM store.shops AS s
                WHERE lower(o.source_module) = 'store'
                  AND o.source_reference_id = s."Id"
                  AND o.source_owner_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_orders_source_owner_created_at",
                schema: "orders",
                table: "orders",
                columns: new[] { "source_module", "source_owner_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_source_owner_created_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "source_owner_id",
                schema: "orders",
                table: "orders");
        }
    }
}
