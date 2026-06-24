using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddShopProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_product_variants",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    DiscountedPriceMinor = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_product_variants",
                schema: "store");
        }
    }
}
