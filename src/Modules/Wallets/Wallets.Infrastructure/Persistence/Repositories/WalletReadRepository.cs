using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Features.GetBalance;
using Wallets.Application.Contracts.Features.GetTransactions;
using Wallets.Application.Contracts.Repositories;

namespace Wallets.Infrastructure.Persistence.Repositories;

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
}
