using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.References.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReferencesCitySlugCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cities_Slug",
                schema: "references",
                table: "cities");

            migrationBuilder.CreateIndex(
                name: "IX_cities_ProvinceId_Slug",
                schema: "references",
                table: "cities",
                columns: new[] { "ProvinceId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cities_ProvinceId_Slug",
                schema: "references",
                table: "cities");

            migrationBuilder.CreateIndex(
                name: "IX_cities_Slug",
                schema: "references",
                table: "cities",
                column: "Slug",
                unique: true);
        }
    }
}
