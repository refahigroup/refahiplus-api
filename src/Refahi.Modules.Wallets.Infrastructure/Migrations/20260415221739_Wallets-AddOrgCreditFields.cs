using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalletsAddOrgCreditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allowed_category_code",
                schema: "wallets",
                table: "wallets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "contract_expires_at",
                schema: "wallets",
                table: "wallets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allowed_category_code",
                schema: "wallets",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "contract_expires_at",
                schema: "wallets",
                table: "wallets");
        }
    }
}
