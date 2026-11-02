using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Orders_PromotionRecoveryLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_promotion_operations_CreatedAt",
                schema: "orders",
                table: "promotion_operations",
                column: "CreatedAt",
                filter: "\"ResultJson\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_promotion_operations_CreatedAt",
                schema: "orders",
                table: "promotion_operations");
        }
    }
}
