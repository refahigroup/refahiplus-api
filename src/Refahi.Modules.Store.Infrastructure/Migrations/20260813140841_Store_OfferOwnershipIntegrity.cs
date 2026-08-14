using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_OfferOwnershipIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_offers_product_sessions_ProductSessionId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_product_variants_ProductVariantId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_products_ProductId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_shops_ShopId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_ProductSessionId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_ProductVariantId",
                schema: "store",
                table: "offers");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "store",
                table: "offers",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM store.offers o
                        JOIN store.products p ON p."Id" = o."ProductId"
                        JOIN store.shops s ON s."Id" = o."ShopId"
                        WHERE p."SupplierId" = '00000000-0000-0000-0000-000000000000'
                           OR s."SupplierId" = '00000000-0000-0000-0000-000000000000'
                           OR p."SupplierId" <> s."SupplierId"
                    ) THEN
                        RAISE EXCEPTION 'Store offer ownership audit failed: Product and Shop suppliers must match';
                    END IF;
                END $$;

                UPDATE store.offers o
                SET "SupplierId" = p."SupplierId"
                FROM store.products p
                WHERE p."Id" = o."ProductId";
                """
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                schema: "store",
                table: "offers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_shops_Id_SupplierId",
                schema: "store",
                table: "shops",
                columns: new[] { "Id", "SupplierId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_products_Id_SupplierId",
                schema: "store",
                table: "products",
                columns: new[] { "Id", "SupplierId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_product_variants_Id_ProductId",
                schema: "store",
                table: "product_variants",
                columns: new[] { "Id", "ProductId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_product_sessions_Id_ProductId",
                schema: "store",
                table: "product_sessions",
                columns: new[] { "Id", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductId_SupplierId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductSessionId_ProductId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductSessionId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ProductVariantId_ProductId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductVariantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_ShopId_SupplierId",
                schema: "store",
                table: "offers",
                columns: new[] { "ShopId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_SupplierId",
                schema: "store",
                table: "offers",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_offers_product_sessions_ProductSessionId_ProductId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductSessionId", "ProductId" },
                principalSchema: "store",
                principalTable: "product_sessions",
                principalColumns: new[] { "Id", "ProductId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_product_variants_ProductVariantId_ProductId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductVariantId", "ProductId" },
                principalSchema: "store",
                principalTable: "product_variants",
                principalColumns: new[] { "Id", "ProductId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_products_ProductId_SupplierId",
                schema: "store",
                table: "offers",
                columns: new[] { "ProductId", "SupplierId" },
                principalSchema: "store",
                principalTable: "products",
                principalColumns: new[] { "Id", "SupplierId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_shops_ShopId_SupplierId",
                schema: "store",
                table: "offers",
                columns: new[] { "ShopId", "SupplierId" },
                principalSchema: "store",
                principalTable: "shops",
                principalColumns: new[] { "Id", "SupplierId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_offers_product_sessions_ProductSessionId_ProductId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_product_variants_ProductVariantId_ProductId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_products_ProductId_SupplierId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropForeignKey(
                name: "FK_offers_shops_ShopId_SupplierId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_shops_Id_SupplierId",
                schema: "store",
                table: "shops");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_products_Id_SupplierId",
                schema: "store",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_product_variants_Id_ProductId",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_product_sessions_Id_ProductId",
                schema: "store",
                table: "product_sessions");

            migrationBuilder.DropIndex(
                name: "IX_offers_ProductId_SupplierId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_ProductSessionId_ProductId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_ProductVariantId_ProductId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_ShopId_SupplierId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "IX_offers_SupplierId",
                schema: "store",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "store",
                table: "offers");

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

            migrationBuilder.AddForeignKey(
                name: "FK_offers_product_sessions_ProductSessionId",
                schema: "store",
                table: "offers",
                column: "ProductSessionId",
                principalSchema: "store",
                principalTable: "product_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_product_variants_ProductVariantId",
                schema: "store",
                table: "offers",
                column: "ProductVariantId",
                principalSchema: "store",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_products_ProductId",
                schema: "store",
                table: "offers",
                column: "ProductId",
                principalSchema: "store",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offers_shops_ShopId",
                schema: "store",
                table: "offers",
                column: "ShopId",
                principalSchema: "store",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
