using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Commerce.Infrastructure.Persistence;

#nullable disable

namespace Refahi.Modules.Commerce.Infrastructure.Migrations;

[DbContext(typeof(CommerceDbContext))]
[Migration("20260824090000_CommerceCartPresentation")]
public partial class CommerceCartPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAvailable", schema: "commerce", table: "cart_items",
            type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>(
            name: "OptionTitle", schema: "commerce", table: "cart_items",
            type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<long>(
            name: "OriginalUnitPriceMinor", schema: "commerce", table: "cart_items",
            type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<string>(
            name: "ProductImageUrl", schema: "commerce", table: "cart_items",
            type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ProductTitle", schema: "commerce", table: "cart_items",
            type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "SellerTitle", schema: "commerce", table: "cart_items",
            type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE commerce.cart_items
            SET "ProductTitle" = "Title",
                "SellerTitle" = "SellerKey",
                "OptionTitle" = "PurchaseOptionKey",
                "OriginalUnitPriceMinor" = "ExpectedUnitPriceMinor"
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsAvailable", schema: "commerce", table: "cart_items");
        migrationBuilder.DropColumn(name: "OptionTitle", schema: "commerce", table: "cart_items");
        migrationBuilder.DropColumn(name: "OriginalUnitPriceMinor", schema: "commerce", table: "cart_items");
        migrationBuilder.DropColumn(name: "ProductImageUrl", schema: "commerce", table: "cart_items");
        migrationBuilder.DropColumn(name: "ProductTitle", schema: "commerce", table: "cart_items");
        migrationBuilder.DropColumn(name: "SellerTitle", schema: "commerce", table: "cart_items");
    }
}
