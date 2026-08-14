using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_RemoveLegacyProductOfferModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_product_variants",
                schema: "store");

            migrationBuilder.DropTable(
                name: "shop_products",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_products_AgreementProductId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "AgreementProductId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DiscountedPriceMinor",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "PriceMinor",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "PriceAdjustment",
                schema: "store",
                table: "product_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgreementProductId",
                schema: "store",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "DiscountedPriceMinor",
                schema: "store",
                table: "product_variants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PriceMinor",
                schema: "store",
                table: "product_variants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PriceAdjustment",
                schema: "store",
                table: "product_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "shop_products",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DiscountedPrice = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_product_variants",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DiscountedPriceMinor = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_product_variants_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "store",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_product_variants_shop_products_ShopProductId",
                        column: x => x.ShopProductId,
                        principalSchema: "store",
                        principalTable: "shop_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_AgreementProductId",
                schema: "store",
                table: "products",
                column: "AgreementProductId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_product_variants_IsDeleted",
                schema: "store",
                table: "shop_product_variants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_shop_product_variants_ProductVariantId",
                schema: "store",
                table: "shop_product_variants",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_product_variants_ShopProductId",
                schema: "store",
                table: "shop_product_variants",
                column: "ShopProductId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_product_variants_ShopProductId_ProductVariantId",
                schema: "store",
                table: "shop_product_variants",
                columns: new[] { "ShopProductId", "ProductVariantId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

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
    }
}
