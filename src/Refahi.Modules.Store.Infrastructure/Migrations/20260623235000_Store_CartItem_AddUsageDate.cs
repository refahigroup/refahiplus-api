using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    [DbContext(typeof(StoreDbContext))]
    [Migration("20260623235000_Store_CartItem_AddUsageDate")]
    public partial class Store_CartItem_AddUsageDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "UsageDate",
                schema: "store",
                table: "cart_items",
                type: "date",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsageDate",
                schema: "store",
                table: "cart_items");
        }
    }
}
