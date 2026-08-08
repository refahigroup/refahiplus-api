using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations;

[DbContext(typeof(WalletsDbContext))]
[Migration("20260808120000_Wallets_SeedStoreSystemWallets")]
public sealed class WalletsSeedStoreSystemWallets : Migration
{
    private const string RevenueWalletId = "7525031a-748a-498b-8538-ad9f1625d5e4";
    private const string VatWalletId = "96014c03-bbdb-4a64-a13b-5df37e643c13";
    private const string PlatformOwnerId = "00000000-0000-0000-0000-000000000001";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            INSERT INTO wallets.wallets
                (wallet_id, "OwnerId", wallet_type, status, currency, created_at)
            VALUES
                ('{{RevenueWalletId}}', '{{PlatformOwnerId}}', 1, 1, 'IRR', NOW()),
                ('{{VatWalletId}}', '{{PlatformOwnerId}}', 1, 1, 'IRR', NOW())
            ON CONFLICT (wallet_id) DO NOTHING;

            INSERT INTO wallets.wallet_balances
                (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
            SELECT wallet_id, 0, 0, currency, NULL, 0, NOW()
            FROM wallets.wallets
            ON CONFLICT (wallet_id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DELETE FROM wallets.wallet_balances wb
            WHERE wb.wallet_id IN ('{{RevenueWalletId}}', '{{VatWalletId}}')
              AND NOT EXISTS (
                  SELECT 1 FROM wallets.ledger_entries le WHERE le.wallet_id = wb.wallet_id);

            DELETE FROM wallets.wallets w
            WHERE w.wallet_id IN ('{{RevenueWalletId}}', '{{VatWalletId}}')
              AND NOT EXISTS (
                  SELECT 1 FROM wallets.ledger_entries le WHERE le.wallet_id = w.wallet_id);
            """);
    }
}
