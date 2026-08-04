using Microsoft.EntityFrameworkCore.Migrations;

using System;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wallets_AddProviderDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "destination_wallet_id",
                schema: "wallets",
                table: "payment_intents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_wallet_id",
                schema: "wallets",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_ledger_entry_id",
                schema: "wallets",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_intents_destination_wallet",
                schema: "wallets",
                table: "payment_intents",
                column: "destination_wallet_id");

            migrationBuilder.CreateIndex(
                name: "idx_payments_destination_wallet",
                schema: "wallets",
                table: "payments",
                column: "destination_wallet_id");

            migrationBuilder.CreateIndex(
                name: "ux_wallets_provider_owner_currency",
                schema: "wallets",
                table: "wallets",
                columns: new[] { "OwnerId", "currency" },
                unique: true,
                filter: "wallet_type = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_intents_destination_wallet",
                schema: "wallets",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "idx_payments_destination_wallet",
                schema: "wallets",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ux_wallets_provider_owner_currency",
                schema: "wallets",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "destination_wallet_id",
                schema: "wallets",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "destination_ledger_entry_id",
                schema: "wallets",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "destination_wallet_id",
                schema: "wallets",
                table: "payments");
        }
    }
}
