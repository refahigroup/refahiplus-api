using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariantValidityCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                schema: "store",
                table: "product_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "CapacityType",
                schema: "store",
                table: "product_variants",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FromDate",
                schema: "store",
                table: "product_variants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ToDate",
                schema: "store",
                table: "product_variants",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId_CapacityType",
                schema: "store",
                table: "product_variants",
                columns: new[] { "ProductId", "CapacityType" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId_FromDate_ToDate",
                schema: "store",
                table: "product_variants",
                columns: new[] { "ProductId", "FromDate", "ToDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_variants_ProductId_CapacityType",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "IX_product_variants_ProductId_FromDate_ToDate",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "Capacity",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "CapacityType",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "FromDate",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "ToDate",
                schema: "store",
                table: "product_variants");
        }
    }
}
