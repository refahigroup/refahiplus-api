using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreRenameProviderIdToSupplierId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderId",
                schema: "store",
                table: "shops",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_shops_ProviderId",
                schema: "store",
                table: "shops",
                newName: "IX_shops_SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_shops_SupplierId",
                schema: "store",
                table: "shops",
                newName: "IX_shops_ProviderId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "store",
                table: "shops",
                newName: "ProviderId");
        }
    }
}
