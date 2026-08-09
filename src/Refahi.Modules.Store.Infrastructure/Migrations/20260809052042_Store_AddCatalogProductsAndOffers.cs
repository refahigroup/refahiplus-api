using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddCatalogProductsAndOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                schema: "store",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "FulfillmentMethod",
                schema: "store",
                table: "products",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

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

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "store",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "offers",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.Id);
                    table.CheckConstraint("CK_offers_discount", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
                    table.CheckConstraint("CK_offers_original_price", "\"OriginalPriceMinor\" > 0");
                    table.CheckConstraint("CK_offers_window", "\"EndDateUtc\" IS NULL OR \"StartDateUtc\" < \"EndDateUtc\"");
                    table.ForeignKey(
                        name: "FK_offers_product_sessions_ProductSessionId",
                        column: x => x.ProductSessionId,
                        principalSchema: "store",
                        principalTable: "product_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offers_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "store",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offers_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "store",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offers_shops_ShopId",
                        column: x => x.ShopId,
                        principalSchema: "store",
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_SupplierId_CategoryId",
                schema: "store",
                table: "products",
                columns: new[] { "SupplierId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductId",
                schema: "store",
                table: "offers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductId_ShopId_IsActive_IsDeleted_StartDateUtc_End~",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductId", "ShopId", "IsActive", "IsDeleted", "StartDateUtc", "EndDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductSessionId",
                schema: "store",
                table: "offers",
                column: "ProductSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductVariantId",
                schema: "store",
                table: "offers",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_offers_ShopId",
                schema: "store",
                table: "offers",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "UX_offers_open_coordinate",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductId", "ShopId", "ProductVariantId", "ProductSessionId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"EndDateUtc\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offers",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_products_SupplierId_CategoryId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FulfillmentMethod",
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

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "store",
                table: "products");
        }
    }
}
