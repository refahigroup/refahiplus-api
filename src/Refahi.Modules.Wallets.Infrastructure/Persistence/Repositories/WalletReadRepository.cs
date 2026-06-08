using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper-based read model access (CQRS read side).
/// </summary>
public sealed class WalletReadRepository : IWalletReadRepository
{
    private readonly string _connectionString;

    public WalletReadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WalletBalanceResponse?> GetWalletBalanceAsync(Guid walletId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var wallet = await conn.QuerySingleOrDefaultAsync<(Guid WalletId, string Currency)>(
            @"select wallet_id as WalletId, currency as Currency
              from wallets.wallets
              where wallet_id = @WalletId",
            new { WalletId = walletId });

        if (wallet == default)
            return null;

        var balance = await conn.QuerySingleOrDefaultAsync<WalletBalanceResponse>(
            @"select
                wallet_id as WalletId,
                currency as Currency,
                available_minor as AvailableMinor,
                pending_minor as PendingMinor,
                version as Version,
                updated_at as UpdatedAt
              from wallets.wallet_balances
              where wallet_id = @WalletId",
            new { WalletId = walletId });

        if (balance is not null)
            return balance;

        return new WalletBalanceResponse(
            WalletId: walletId,
            Currency: wallet.Currency,
            AvailableMinor: 0,
            PendingMinor: 0,
            Version: 0,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<GetTransactionsResponse>?> GetWalletTransactionsAsync(Guid walletId, int take)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var walletExists = await conn.ExecuteScalarAsync<bool>(
            @"select exists(select 1 from wallets.wallets where wallet_id = @WalletId)",
            new { WalletId = walletId });

        if (!walletExists)
            return null;

        var rows = await conn.QueryAsync<GetTransactionsResponse>(
            @"select
                ledger_entry_id as LedgerEntryId,
                operation_id as OperationId,
                operation_type as OperationType,
                entry_type as EntryType,
                amount_minor as AmountMinor,
                currency as Currency,
                effective_at as EffectiveAt,
                created_at as CreatedAt,
                related_entry_id as RelatedEntryId,
                relation_type as RelationType,
                external_reference as ExternalReference
              from wallets.ledger_entries
              where wallet_id = @WalletId
              order by created_at desc
              limit @Take",
            new { WalletId = walletId, Take = take });

        return rows.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<MyWalletTransactionDto>> GetOwnerWalletTransactionsAsync(
        Guid ownerId,
        int take,
        string? walletType = null,
        short? operationType = null,
        short? entryType = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        short? walletTypeValue = walletType?.Trim() switch
        {
            null or "" => null,
            "REFAHI" or "Refahi" or "User" => (short)WalletType.User,
            "OrgCredit" or "ORG_CREDIT" or "Organizational" => (short)WalletType.OrgCredit,
            _ when short.TryParse(walletType, out var parsed) => parsed,
            _ => null
        };

        var rows = await conn.QueryAsync<MyWalletTransactionDto>(
            """
            SELECT
                le.ledger_entry_id AS LedgerEntryId,
                le.wallet_id AS WalletId,
                CASE
                    WHEN w.wallet_type = @UserWalletType THEN 'REFAHI'
                    WHEN w.wallet_type = @OrgCreditWalletType THEN 'OrgCredit'
                    WHEN w.wallet_type = @SystemWalletType THEN 'System'
                    WHEN w.wallet_type = @ProviderWalletType THEN 'Provider'
                    ELSE 'Unknown'
                END AS WalletType,
                le.operation_id AS OperationId,
                le.operation_type AS OperationType,
                le.entry_type AS EntryType,
                le.amount_minor AS AmountMinor,
                le.currency AS Currency,
                le.effective_at AS EffectiveAt,
                le.created_at AS CreatedAt,
                le.related_entry_id AS RelatedEntryId,
                le.relation_type AS RelationType,
                le.external_reference AS ExternalReference
            FROM wallets.ledger_entries le
            INNER JOIN wallets.wallets w ON w.wallet_id = le.wallet_id
            WHERE w."OwnerId" = @OwnerId
              AND (@WalletType IS NULL OR w.wallet_type = @WalletType)
              AND (@OperationType IS NULL OR le.operation_type = @OperationType)
              AND (@EntryType IS NULL OR le.entry_type = @EntryType)
            ORDER BY le.created_at DESC
            LIMIT @Take
            """,
            new
            {
                OwnerId = ownerId,
                Take = take,
                WalletType = walletTypeValue,
                UserWalletType = (short)WalletType.User,
                OrgCreditWalletType = (short)WalletType.OrgCredit,
                SystemWalletType = (short)WalletType.System,
                ProviderWalletType = (short)WalletType.Provider,
                OperationType = operationType,
                EntryType = entryType
            });

        return rows.ToList().AsReadOnly();
    }

    public async Task<List<WalletSummaryDto>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<(Guid WalletId, short WalletType, string Currency, long AvailableMinor, long PendingMinor, string? AllowedCategoryCode, DateTimeOffset? ContractExpiresAt)>(
            """
            SELECT w.wallet_id, w.wallet_type, w.currency,
                   COALESCE(wb.available_minor, 0) AS available_minor,
                   COALESCE(wb.pending_minor, 0) AS pending_minor,
                   w.allowed_category_code,
                   w.contract_expires_at
            FROM wallets.wallets w
            LEFT JOIN wallets.wallet_balances wb ON wb.wallet_id = w.wallet_id
            WHERE w."OwnerId" = @OwnerId
            ORDER BY w.created_at
            """,
            new { OwnerId = ownerId });

        return rows.Select(r => new WalletSummaryDto(
            WalletId: r.WalletId,
            WalletType: r.WalletType == (short)WalletType.User ? "REFAHI" : ((WalletType)r.WalletType).ToString(),
            Currency: r.Currency,
            AvailableBalanceMinor: r.AvailableMinor,
            TotalBalanceMinor: r.AvailableMinor + r.PendingMinor,
            HeldAmountMinor: r.PendingMinor,
            AllowedCategoryCode: r.AllowedCategoryCode,
            ContractExpiresAt: r.ContractExpiresAt
        )).ToList();
    }

    public async Task<bool> ExistsByOwnerAndTypeAsync(Guid ownerId, short walletType, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM wallets.wallets WHERE \"OwnerId\" = @OwnerId AND wallet_type = @WalletType)",
            new { OwnerId = ownerId, WalletType = walletType });
    }

    public async Task<WalletInfoDto?> GetByIdAsync(Guid walletId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<(Guid WalletId, short WalletType, short Status, string Currency, string? AllowedCategoryCode, DateTimeOffset? ContractExpiresAt)>(
            """
            SELECT wallet_id, wallet_type, status, currency, allowed_category_code, contract_expires_at
            FROM wallets.wallets
            WHERE wallet_id = @WalletId
            """,
            new { WalletId = walletId });

        if (row == default)
            return null;

        return new WalletInfoDto(
            WalletId: row.WalletId,
            WalletType: row.WalletType,
            Status: row.Status,
            Currency: row.Currency,
            AllowedCategoryCode: row.AllowedCategoryCode,
            ContractExpiresAt: row.ContractExpiresAt);
    }
}
