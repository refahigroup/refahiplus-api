using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;

public sealed class WalletWriteRepository : IWalletWriteRepository
{
    private readonly string _connectionString;

    public WalletWriteRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> CreateAsync(Guid ownerId, short walletType, short walletStatus, string currency, CancellationToken ct = default)
    {
        var walletId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(
            """
            INSERT INTO wallets.wallets (wallet_id, "OwnerId", wallet_type, status, currency, created_at)
            VALUES (@WalletId, @OwnerId, @WalletType, @Status, @Currency, @CreatedAt)
            """,
            new { WalletId = walletId, OwnerId = ownerId, WalletType = walletType, Status = walletStatus, Currency = currency, CreatedAt = now },
            transaction);

        await conn.ExecuteAsync(
            """
            INSERT INTO wallets.wallet_balances
                (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
            VALUES (@WalletId, 0, 0, @Currency, NULL, 0, @UpdatedAt)
            """,
            new { WalletId = walletId, Currency = currency, UpdatedAt = now },
            transaction);

        await transaction.CommitAsync(ct);

        return walletId;
    }

    public async Task<Guid> CreateOrgCreditAsync(Guid ownerId, string currency, string? allowedCategoryCode, DateTimeOffset? contractExpiresAt, CancellationToken ct = default)
    {
        var walletId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(
            """
            INSERT INTO wallets.wallets (wallet_id, "OwnerId", wallet_type, status, currency, allowed_category_code, contract_expires_at, created_at)
            VALUES (@WalletId, @OwnerId, @WalletType, @Status, @Currency, @AllowedCategoryCode, @ContractExpiresAt, @CreatedAt)
            """,
            new
            {
                WalletId = walletId,
                OwnerId = ownerId,
                WalletType = (short)WalletType.OrgCredit,
                Status = (short)WalletStatus.Active,
                Currency = currency.ToUpperInvariant(),
                AllowedCategoryCode = allowedCategoryCode,
                ContractExpiresAt = contractExpiresAt,
                CreatedAt = now
            },
            transaction);

        await conn.ExecuteAsync(
            """
            INSERT INTO wallets.wallet_balances
                (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
            VALUES (@WalletId, 0, 0, @Currency, NULL, 0, @UpdatedAt)
            """,
            new { WalletId = walletId, Currency = currency.ToUpperInvariant(), UpdatedAt = now },
            transaction);

        await transaction.CommitAsync(ct);

        return walletId;
    }
}
