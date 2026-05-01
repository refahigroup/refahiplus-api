using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_ShopProduct_AddPricing_StoreModule_AddCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                schema: "store",
                table: "shop_products",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "CommissionPrice",
                schema: "store",
                table: "shop_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "DiscountedPrice",
                schema: "store",
                table: "shop_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Price",
                schema: "store",
                table: "shop_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                schema: "store",
                table: "modules",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                schema: "store",
                table: "shop_products");

            migrationBuilder.DropColumn(
                name: "CommissionPrice",
                schema: "store",
                table: "shop_products");

            migrationBuilder.DropColumn(
                name: "DiscountedPrice",
                schema: "store",
                table: "shop_products");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "store",
                table: "shop_products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "store",
                table: "modules");
        }
    }
}
