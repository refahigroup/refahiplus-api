using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Interfaces;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

/// <summary>
/// Rebuilds wallet_balances projection from ledger entries (source of truth).
/// Implements deterministic reconciliation based on OperationType rules.
/// </summary>
public sealed class BalanceRebuilder : IBalanceRebuilder
{
    private readonly string _connectionString;
    private readonly ILogger<BalanceRebuilder> _logger;

    public BalanceRebuilder(string connectionString, ILogger<BalanceRebuilder> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<RebuildBalanceResult> RebuildSingleWalletAsync(Guid walletId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;

        // 1) Load current projection (before)
        var before = await LoadCurrentProjectionAsync(conn, tx, walletId, ct);

        // 2) Compute from ledger (true balance)
        var computed = await ComputeBalanceFromLedgerAsync(conn, tx, walletId, ct);

        // 3) Detect drift
        var drift = new DriftInfo(
            HasDrift: before.AvailableMinor != computed.AvailableMinor || before.PendingMinor != computed.PendingMinor,
            AvailableDelta: computed.AvailableMinor - before.AvailableMinor,
            PendingDelta: computed.PendingMinor - before.PendingMinor,
            VersionDelta: 0); // Version will increment after upsert

        // 4) Upsert computed balance (replace projection)
        var afterRow = await UpsertBalanceAsync(conn, tx, walletId, computed, now, ct);
        var after = new BalanceSnapshot(afterRow.AvailableMinor, afterRow.PendingMinor, afterRow.Version, afterRow.UpdatedAt);

        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Rebuilt balance for wallet {WalletId}: Available {AvailableDelta:+#;-#;0}, Pending {PendingDelta:+#;-#;0}",
            walletId, drift.AvailableDelta, drift.PendingDelta);

        return new RebuildBalanceResult(
            WalletId: walletId,
            Currency: computed.Currency,
            Before: before,
            After: after,
            Drift: drift,
            RebuiltAt: now);
    }

    public async Task<BatchRebuildResult> RebuildBatchAsync(BatchRebuildFilters? filters, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var details = new List<WalletRebuildSummary>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Load all wallet IDs matching filters
        var walletIds = await LoadWalletIdsAsync(conn, filters, ct);

        _logger.LogInformation("Starting batch rebuild for {Count} wallets", walletIds.Count);

        int successCount = 0, driftCount = 0, failureCount = 0;

        foreach (var walletId in walletIds)
        {
            try
            {
                var result = await RebuildSingleWalletAsync(walletId, ct);
                
                successCount++;
                if (result.Drift.HasDrift)
                    driftCount++;

                details.Add(new WalletRebuildSummary(
                    WalletId: walletId,
                    Success: true,
                    HadDrift: result.Drift.HasDrift,
                    ErrorMessage: null));
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "Failed to rebuild wallet {WalletId}", walletId);
                
                details.Add(new WalletRebuildSummary(
                    WalletId: walletId,
                    Success: false,
                    HadDrift: false,
                    ErrorMessage: ex.Message));
            }
        }

        return new BatchRebuildResult(
            TotalWallets: walletIds.Count,
            SuccessCount: successCount,
            DriftDetectedCount: driftCount,
            FailureCount: failureCount,
            Details: details,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow);
    }

    public async Task<DriftReport> DetectDriftAsync(Guid walletId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var current = await LoadCurrentProjectionAsync(conn, null, walletId, ct);
        var computed = await ComputeBalanceFromLedgerAsync(conn, null, walletId, ct);
        var computedSnapshot = new BalanceSnapshot(computed.AvailableMinor, computed.PendingMinor, 0, DateTimeOffset.UtcNow);

        var drift = new DriftInfo(
            HasDrift: current.AvailableMinor != computed.AvailableMinor || current.PendingMinor != computed.PendingMinor,
            AvailableDelta: computed.AvailableMinor - current.AvailableMinor,
            PendingDelta: computed.PendingMinor - current.PendingMinor,
            VersionDelta: 0);

        return new DriftReport(
            WalletId: walletId,
            Currency: computed.Currency,
            CurrentProjection: current,
            ComputedFromLedger: computedSnapshot,
            Drift: drift);
    }

    // ========================================================================
    // PRIVATE HELPERS
    // ========================================================================

    private async Task<BalanceSnapshot> LoadCurrentProjectionAsync(
        NpgsqlConnection conn, 
        NpgsqlTransaction? tx, 
        Guid walletId, 
        CancellationToken ct)
    {
        var row = await conn.QuerySingleOrDefaultAsync<BalanceRow>(new CommandDefinition(
            Sql.SelectCurrentBalance,
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));

        return row is not null
            ? new BalanceSnapshot(row.AvailableMinor, row.PendingMinor, row.Version, row.UpdatedAt)
            : new BalanceSnapshot(0, 0, 0, DateTimeOffset.MinValue); // Empty wallet
    }

    private async Task<ComputedBalance> ComputeBalanceFromLedgerAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? tx,
        Guid walletId,
        CancellationToken ct)
    {
        // Get wallet currency first
        var currency = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "select currency from wallets.wallets where wallet_id = @WalletId",
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));

        if (string.IsNullOrEmpty(currency))
            throw new InvalidOperationException($"Wallet {walletId} not found");

        // Aggregate ledger by operation_type
        var aggregates = await conn.QueryAsync<LedgerAggregateRow>(new CommandDefinition(
            Sql.AggregateByOperationType,
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));

        var lookup = aggregates.ToDictionary(x => x.OperationType, x => x.TotalAmount);

        long available = 0;
        long pending = 0;

        // Apply operation type rules deterministically
        // TopUp (1): available += amount
        if (lookup.TryGetValue(1, out var topUp))
            available += topUp;

        // Reserve (2): available -= amount, pending += amount
        if (lookup.TryGetValue(2, out var reserve))
        {
            available -= reserve;
            pending += reserve;
        }

        // Payment (3): pending -= amount
        if (lookup.TryGetValue(3, out var payment))
            pending -= payment;

        // Release (4): available += amount, pending -= amount
        if (lookup.TryGetValue(4, out var release))
        {
            available += release;
            pending -= release;
        }

        // Refund (5): available += amount
        if (lookup.TryGetValue(5, out var refund))
            available += refund;

        return new ComputedBalance(currency, available, pending);
    }

    private async Task<BalanceRow> UpsertBalanceAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid walletId,
        ComputedBalance computed,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.UpsertBalance,
            new
            {
                WalletId = walletId,
                AvailableMinor = computed.AvailableMinor,
                PendingMinor = computed.PendingMinor,
                Currency = computed.Currency,
                UpdatedAt = now
            },
            tx,
            cancellationToken: ct));

        // Return updated snapshot
        var row = await conn.QuerySingleAsync<BalanceRow>(new CommandDefinition(
            Sql.SelectCurrentBalance,
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));
        
        return row;
    }

    private async Task<List<Guid>> LoadWalletIdsAsync(
        NpgsqlConnection conn,
        BatchRebuildFilters? filters,
        CancellationToken ct)
    {
        var query = "select wallet_id from wallets.wallets where 1=1";
        var parameters = new DynamicParameters();

        if (filters?.Currency is not null)
        {
            query += " and currency = @Currency";
            parameters.Add("Currency", filters.Currency);
        }

        if (filters?.OnlyActive == true)
        {
            query += " and status = 1"; // Active status
        }

        var ids = await conn.QueryAsync<Guid>(new CommandDefinition(query, parameters, cancellationToken: ct));
        return ids.ToList();
    }

    // ========================================================================
    // SQL CONSTANTS
    // ========================================================================
    private static class Sql
    {
        public const string SelectCurrentBalance = @"
select available_minor as AvailableMinor, pending_minor as PendingMinor,
       version as Version, updated_at as UpdatedAt
from wallets.wallet_balances
where wallet_id = @WalletId;
";

        public const string AggregateByOperationType = @"
select operation_type as OperationType, sum(amount_minor) as TotalAmount
from wallets.ledger_entries
where wallet_id = @WalletId
group by operation_type;
";

        public const string UpsertBalance = @"
insert into wallets.wallet_balances (
  wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, updated_at, version
)
values (
  @WalletId, @AvailableMinor, @PendingMinor, @Currency, null, @UpdatedAt, 1
)
on conflict (wallet_id) do update
set
  available_minor = excluded.available_minor,
  pending_minor = excluded.pending_minor,
  updated_at = excluded.updated_at,
  version = wallets.wallet_balances.version + 1;
";
    }

    // ========================================================================
    // RECORD TYPES
    // ========================================================================
    private record BalanceRow(long AvailableMinor, long PendingMinor, long Version, DateTimeOffset UpdatedAt);
    private record ComputedBalance(string Currency, long AvailableMinor, long PendingMinor);
    private record LedgerAggregateRow(short OperationType, long TotalAmount);
}
