using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Organizations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Organizations_MembershipLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_memberships_UserId_ValidFrom",
                schema: "organizations",
                table: "memberships",
                columns: new[] { "UserId", "ValidFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_UserId_ValidFrom",
                schema: "organizations",
                table: "memberships");
        }
    }
}
