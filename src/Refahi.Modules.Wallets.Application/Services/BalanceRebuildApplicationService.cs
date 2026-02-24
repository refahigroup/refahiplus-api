using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts.Interfaces;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Services;

/// <summary>
/// Application service for balance rebuild operations.
/// Orchestrates IBalanceRebuilder and maps to response DTOs.
/// </summary>
public sealed class BalanceRebuildApplicationService
{
    private readonly IBalanceRebuilder _rebuilder;

    public BalanceRebuildApplicationService(IBalanceRebuilder rebuilder)
    {
        _rebuilder = rebuilder;
    }

    public async Task<CommandResponse<RebuildBalanceResponse>> RebuildBalanceAsync(
        RebuildBalanceCommand command,
        CancellationToken ct)
    {
        var result = await _rebuilder.RebuildSingleWalletAsync(command.WalletId, ct);

        var response = new RebuildBalanceResponse(
            WalletId: result.WalletId,
            Currency: result.Currency,
            Before: MapSnapshot(result.Before),
            After: MapSnapshot(result.After),
            Drift: MapDrift(result.Drift),
            RebuiltAt: result.RebuiltAt);

        return new CommandResponse<RebuildBalanceResponse>(CommandStatus.Completed, response);
    }

    public async Task<CommandResponse<BatchRebuildResponse>> RebuildBatchAsync(
        RebuildBalancesBatchCommand command,
        CancellationToken ct)
    {
        var filters = new BatchRebuildFilters(
            Currency: command.Currency,
            OnlyActive: command.OnlyActive);

        var result = await _rebuilder.RebuildBatchAsync(filters, ct);

        var duration = (result.CompletedAt - result.StartedAt).TotalSeconds;

        var response = new BatchRebuildResponse(
            TotalWallets: result.TotalWallets,
            SuccessCount: result.SuccessCount,
            DriftDetectedCount: result.DriftDetectedCount,
            FailureCount: result.FailureCount,
            Details: result.Details.Select(d => new WalletRebuildSummaryResponse(
                d.WalletId, d.Success, d.HadDrift, d.ErrorMessage)).ToList(),
            StartedAt: result.StartedAt,
            CompletedAt: result.CompletedAt,
            DurationSeconds: duration);

        return new CommandResponse<BatchRebuildResponse>(CommandStatus.Completed, response);
    }

    public async Task<CommandResponse<DriftReportResponse>> DetectDriftAsync(
        DetectDriftQuery query,
        CancellationToken ct)
    {
        var result = await _rebuilder.DetectDriftAsync(query.WalletId, ct);

        var response = new DriftReportResponse(
            WalletId: result.WalletId,
            Currency: result.Currency,
            CurrentProjection: MapSnapshot(result.CurrentProjection),
            ComputedFromLedger: MapSnapshot(result.ComputedFromLedger),
            Drift: MapDrift(result.Drift));

        return new CommandResponse<DriftReportResponse>(CommandStatus.Completed, response);
    }

    private static BalanceSnapshotResponse MapSnapshot(BalanceSnapshot snapshot)
        => new(snapshot.AvailableMinor, snapshot.PendingMinor, snapshot.Version, snapshot.UpdatedAt);

    private static DriftInfoResponse MapDrift(DriftInfo drift)
        => new(drift.HasDrift, drift.AvailableDelta, drift.PendingDelta, drift.VersionDelta);
}
