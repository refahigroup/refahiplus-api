using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_InPersonOtpDispatchGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OtpDispatchStartedAt",
                schema: "store",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpDispatchStartedAt",
                schema: "store",
                table: "store_orders"
            );
        }
    }
}
