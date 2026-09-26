using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wallets_AddMerchantWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_wallets_merchant_owner_currency",
                schema: "wallets",
                table: "wallets",
                columns: new[] { "OwnerId", "currency" },
                unique: true,
                filter: "wallet_type = 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_wallets_merchant_owner_currency",
                schema: "wallets",
                table: "wallets");
        }
    }
}
