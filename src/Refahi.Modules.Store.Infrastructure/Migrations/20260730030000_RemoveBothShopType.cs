using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20260730030000_RemoveBothShopType")]
public partial class RemoveBothShopType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM store.shops
                    WHERE "ShopType" NOT IN (1, 2)
                ) THEN
                    RAISE EXCEPTION 'پیش از اجرای migration، نوع فروشگاه‌های Both باید به Online یا Physical تغییر کند.';
                END IF;
            END $$;
            """
        );

        migrationBuilder.AddCheckConstraint(
            name: "CK_shops_shop_type",
            schema: "store",
            table: "shops",
            sql: "\"ShopType\" IN (1, 2)"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_shops_shop_type",
            schema: "store",
            table: "shops"
        );
    }
}
