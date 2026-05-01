using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_AgreementProduct_RemovePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                schema: "supplychain",
                table: "agreement_products");

            migrationBuilder.DropColumn(
                name: "CommissionPrice",
                schema: "supplychain",
                table: "agreement_products");

            migrationBuilder.DropColumn(
                name: "DiscountedPrice",
                schema: "supplychain",
                table: "agreement_products");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "supplychain",
                table: "agreement_products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                schema: "supplychain",
                table: "agreement_products",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "CommissionPrice",
                schema: "supplychain",
                table: "agreement_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "DiscountedPrice",
                schema: "supplychain",
                table: "agreement_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Price",
                schema: "supplychain",
                table: "agreement_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
