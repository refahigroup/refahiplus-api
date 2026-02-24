using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Responses;

/// <summary>
/// Response DTO for refund query (read-only projection).
/// </summary>
public sealed record GetRefundResponse(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    string Status,
    long AmountMinor,
    string Currency,
    List<AllocationDto> Allocations,
    DateTimeOffset CompletedAt,
    string? Reason
);
