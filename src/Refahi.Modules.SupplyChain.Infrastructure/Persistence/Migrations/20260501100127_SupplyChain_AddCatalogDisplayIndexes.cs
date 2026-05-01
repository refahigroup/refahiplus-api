using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_AddCatalogDisplayIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_agreements_SupplierId_Status_ToDate",
                schema: "supplychain",
                table: "agreements",
                columns: new[] { "SupplierId", "Status", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_agreement_products_CategoryId_IsDeleted",
                schema: "supplychain",
                table: "agreement_products",
                columns: new[] { "CategoryId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agreements_SupplierId_Status_ToDate",
                schema: "supplychain",
                table: "agreements");

            migrationBuilder.DropIndex(
                name: "IX_agreement_products_CategoryId_IsDeleted",
                schema: "supplychain",
                table: "agreement_products");
        }
    }
}
