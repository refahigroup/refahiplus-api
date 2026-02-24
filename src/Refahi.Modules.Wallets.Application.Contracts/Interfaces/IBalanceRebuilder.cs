using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Interfaces;

/// <summary>
/// Rebuilds wallet_balances projection from ledger entries (source of truth).
/// Used for reconciliation and drift repair.
/// </summary>
public interface IBalanceRebuilder
{
    /// <summary>
    /// Rebuilds balance for a single wallet by recomputing from ledger.
    /// </summary>
    /// <param name="walletId">Target wallet ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rebuild result with before/after snapshot and drift detection</returns>
    Task<RebuildBalanceResult> RebuildSingleWalletAsync(Guid walletId, CancellationToken ct);

    /// <summary>
    /// Rebuilds balances for all wallets or filtered subset.
    /// </summary>
    /// <param name="filters">Optional filters (currency, date range, etc.)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Batch rebuild summary</returns>
    Task<BatchRebuildResult> RebuildBatchAsync(BatchRebuildFilters? filters, CancellationToken ct);

    /// <summary>
    /// Detects drift between ledger and projection without modifying data.
    /// </summary>
    /// <param name="walletId">Target wallet ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Drift report</returns>
    Task<DriftReport> DetectDriftAsync(Guid walletId, CancellationToken ct);
}

// ============================================================================
// RESULT TYPES
// ============================================================================

public record RebuildBalanceResult(
    Guid WalletId,
    string Currency,
    BalanceSnapshot Before,
    BalanceSnapshot After,
    DriftInfo Drift,
    DateTimeOffset RebuiltAt);

public record BalanceSnapshot(
    long AvailableMinor,
    long PendingMinor,
    long Version,
    DateTimeOffset UpdatedAt);

public record DriftInfo(
    bool HasDrift,
    long AvailableDelta,
    long PendingDelta,
    long VersionDelta);

public record BatchRebuildResult(
    int TotalWallets,
    int SuccessCount,
    int DriftDetectedCount,
    int FailureCount,
    List<WalletRebuildSummary> Details,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public record WalletRebuildSummary(
    Guid WalletId,
    bool Success,
    bool HadDrift,
    string? ErrorMessage);

public record DriftReport(
    Guid WalletId,
    string Currency,
    BalanceSnapshot CurrentProjection,
    BalanceSnapshot ComputedFromLedger,
    DriftInfo Drift);

public record BatchRebuildFilters(
    string? Currency = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null,
    bool OnlyActive = true);
