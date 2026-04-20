using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrdersAddXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "orders",
                table: "orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "orders",
                table: "orders");
        }
    }
}
