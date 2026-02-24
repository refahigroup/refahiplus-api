using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response for single wallet balance rebuild operation.
/// </summary>
public record RebuildBalanceResponse(
    Guid WalletId,
    string Currency,
    BalanceSnapshotResponse Before,
    BalanceSnapshotResponse After,
    DriftInfoResponse Drift,
    DateTimeOffset RebuiltAt);

public record BalanceSnapshotResponse(
    long AvailableMinor,
    long PendingMinor,
    long Version,
    DateTimeOffset UpdatedAt);

public record DriftInfoResponse(
    bool HasDrift,
    long AvailableDelta,
    long PendingDelta,
    long VersionDelta);
