using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response for batch balance rebuild operation.
/// </summary>
public record BatchRebuildResponse(
    int TotalWallets,
    int SuccessCount,
    int DriftDetectedCount,
    int FailureCount,
    List<WalletRebuildSummaryResponse> Details,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    double DurationSeconds);

public record WalletRebuildSummaryResponse(
    Guid WalletId,
    bool Success,
    bool HadDrift,
    string? ErrorMessage);
