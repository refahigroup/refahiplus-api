using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_AgreementProduct_AddTypeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "DeliveryType",
                schema: "supplychain",
                table: "agreement_products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ProductType",
                schema: "supplychain",
                table: "agreement_products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SalesModel",
                schema: "supplychain",
                table: "agreement_products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryType",
                schema: "supplychain",
                table: "agreement_products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                schema: "supplychain",
                table: "agreement_products");

            migrationBuilder.DropColumn(
                name: "SalesModel",
                schema: "supplychain",
                table: "agreement_products");
        }
    }
}
