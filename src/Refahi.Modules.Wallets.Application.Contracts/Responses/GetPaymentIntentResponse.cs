using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response DTO for payment intent query (read-only projection).
/// </summary>
public sealed record GetPaymentIntentResponse(
    Guid IntentId,
    Guid OrderId,
    string Status,
    long AmountMinor,
    string Currency,
    List<AllocationDto> Allocations,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ReleasedAt
);

/// <summary>
/// Allocation DTO for query responses.
/// </summary>
public sealed record AllocationDto(
    Guid WalletId,
    long AmountMinor,
    Guid? LedgerEntryId
);
