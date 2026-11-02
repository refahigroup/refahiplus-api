using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Promotions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Promotions_RecoveryLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_redemptions_Status_CreatedAt",
                schema: "promotions",
                table: "redemptions",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_redemptions_Status_CreatedAt",
                schema: "promotions",
                table: "redemptions");
        }
    }
}
