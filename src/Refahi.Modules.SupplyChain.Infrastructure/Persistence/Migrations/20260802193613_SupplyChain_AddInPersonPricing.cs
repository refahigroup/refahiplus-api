using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_AddInPersonPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PricingMode",
                schema: "supplychain",
                table: "agreement_products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1
            );

            migrationBuilder.AddColumn<bool>(
                name: "VatApplicable",
                schema: "supplychain",
                table: "agreement_products",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.Sql(
                """
                UPDATE supplychain.agreement_products
                SET "PricingMode" = 2, "SalesModel" = 3
                WHERE "DeliveryType" = 3;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE supplychain.agreement_products
                SET "SalesModel" = 1
                WHERE "DeliveryType" = 3 AND "SalesModel" = 3;
                """
            );

            migrationBuilder.DropColumn(
                name: "PricingMode",
                schema: "supplychain",
                table: "agreement_products"
            );

            migrationBuilder.DropColumn(
                name: "VatApplicable",
                schema: "supplychain",
                table: "agreement_products"
            );
        }
    }
}
