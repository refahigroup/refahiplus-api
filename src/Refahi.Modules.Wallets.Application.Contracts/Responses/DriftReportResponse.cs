using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response for drift detection query (read-only, no modification).
/// </summary>
public record DriftReportResponse(
    Guid WalletId,
    string Currency,
    BalanceSnapshotResponse CurrentProjection,
    BalanceSnapshotResponse ComputedFromLedger,
    DriftInfoResponse Drift);
