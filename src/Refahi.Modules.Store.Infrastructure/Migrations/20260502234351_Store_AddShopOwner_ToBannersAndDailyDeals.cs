using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddShopOwner_ToBannersAndDailyDeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_daily_deals_ModuleId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropIndex(
                name: "IX_banners_ModuleId",
                schema: "store",
                table: "banners");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                schema: "store",
                table: "daily_deals",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                schema: "store",
                table: "daily_deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                schema: "store",
                table: "banners",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                schema: "store",
                table: "banners",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_deals_ModuleId",
                schema: "store",
                table: "daily_deals",
                column: "ModuleId",
                filter: "\"ModuleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_daily_deals_ShopId",
                schema: "store",
                table: "daily_deals",
                column: "ShopId",
                filter: "\"ShopId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_daily_deals_owner_xor",
                schema: "store",
                table: "daily_deals",
                sql: "(\"ModuleId\" IS NULL) <> (\"ShopId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_banners_ModuleId",
                schema: "store",
                table: "banners",
                column: "ModuleId",
                filter: "\"ModuleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_banners_ShopId",
                schema: "store",
                table: "banners",
                column: "ShopId",
                filter: "\"ShopId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_banners_owner_xor",
                schema: "store",
                table: "banners",
                sql: "(\"ModuleId\" IS NULL) <> (\"ShopId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_banners_modules_ModuleId",
                schema: "store",
                table: "banners",
                column: "ModuleId",
                principalSchema: "store",
                principalTable: "modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_banners_shops_ShopId",
                schema: "store",
                table: "banners",
                column: "ShopId",
                principalSchema: "store",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_daily_deals_modules_ModuleId",
                schema: "store",
                table: "daily_deals",
                column: "ModuleId",
                principalSchema: "store",
                principalTable: "modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_daily_deals_shops_ShopId",
                schema: "store",
                table: "daily_deals",
                column: "ShopId",
                principalSchema: "store",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_banners_modules_ModuleId",
                schema: "store",
                table: "banners");

            migrationBuilder.DropForeignKey(
                name: "FK_banners_shops_ShopId",
                schema: "store",
                table: "banners");

            migrationBuilder.DropForeignKey(
                name: "FK_daily_deals_modules_ModuleId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropForeignKey(
                name: "FK_daily_deals_shops_ShopId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropIndex(
                name: "IX_daily_deals_ModuleId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropIndex(
                name: "IX_daily_deals_ShopId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_daily_deals_owner_xor",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropIndex(
                name: "IX_banners_ModuleId",
                schema: "store",
                table: "banners");

            migrationBuilder.DropIndex(
                name: "IX_banners_ShopId",
                schema: "store",
                table: "banners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_banners_owner_xor",
                schema: "store",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "ShopId",
                schema: "store",
                table: "daily_deals");

            migrationBuilder.DropColumn(
                name: "ShopId",
                schema: "store",
                table: "banners");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                schema: "store",
                table: "daily_deals",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                schema: "store",
                table: "banners",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_deals_ModuleId",
                schema: "store",
                table: "daily_deals",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_banners_ModuleId",
                schema: "store",
                table: "banners",
                column: "ModuleId");
        }
    }
}
