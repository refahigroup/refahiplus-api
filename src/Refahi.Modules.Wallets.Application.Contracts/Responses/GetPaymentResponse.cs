using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response DTO for payment query (read-only projection).
/// </summary>
public sealed record GetPaymentResponse(
    Guid PaymentId,
    Guid IntentId,
    Guid OrderId,
    string Status,
    long AmountMinor,
    string Currency,
    List<AllocationDto> Allocations,
    DateTimeOffset CompletedAt
);
