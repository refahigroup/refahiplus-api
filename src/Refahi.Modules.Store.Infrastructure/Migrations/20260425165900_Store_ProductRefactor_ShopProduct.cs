using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_ProductRefactor_ShopProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_CategoryId",
                schema: "store",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CityId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DeliveryType",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DiscountedPriceMinor",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PriceMinor",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SalesModel",
                schema: "store",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "ShopId",
                schema: "store",
                table: "products",
                newName: "AgreementProductId");

            migrationBuilder.RenameIndex(
                name: "IX_products_ShopId",
                schema: "store",
                table: "products",
                newName: "IX_products_AgreementProductId");

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                schema: "store",
                table: "cart_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "shop_products",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_ShopId",
                schema: "store",
                table: "cart_items",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_products_IsDeleted",
                schema: "store",
                table: "shop_products",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_shop_products_ProductId",
                schema: "store",
                table: "shop_products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_products_ShopId",
                schema: "store",
                table: "shop_products",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_products_ShopId_ProductId",
                schema: "store",
                table: "shop_products",
                columns: new[] { "ShopId", "ProductId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_products",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_ShopId",
                schema: "store",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "ShopId",
                schema: "store",
                table: "cart_items");

            migrationBuilder.RenameColumn(
                name: "AgreementProductId",
                schema: "store",
                table: "products",
                newName: "ShopId");

            migrationBuilder.RenameIndex(
                name: "IX_products_AgreementProductId",
                schema: "store",
                table: "products",
                newName: "IX_products_ShopId");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                schema: "store",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryCode",
                schema: "store",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                schema: "store",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "store",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                schema: "store",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                schema: "store",
                table: "products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<short>(
                name: "DeliveryType",
                schema: "store",
                table: "products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                schema: "store",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiscountedPriceMinor",
                schema: "store",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PriceMinor",
                schema: "store",
                table: "products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<short>(
                name: "ProductType",
                schema: "store",
                table: "products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SalesModel",
                schema: "store",
                table: "products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId",
                schema: "store",
                table: "products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_CityId",
                schema: "store",
                table: "products",
                column: "CityId");
        }
    }
}
