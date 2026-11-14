using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolsFinancialCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OrderCancellationCompleted",
                schema: "pools",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderCancellationCompleted",
                schema: "pools",
                table: "bookings");
        }
    }
}
