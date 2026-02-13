using System;
using System.Collections.Generic;

namespace Wallets.Application.Contracts.Features.CreatePaymentIntent;

/// <summary>
/// Response for Create Payment Intent.
/// </summary>
public sealed record CreatePaymentIntentResponse(
    Guid IntentId,
    Guid OrderId,
    long AmountMinor,
    string Currency,
    string Status,
    List<AllocationResponse> Allocations,
    DateTimeOffset CreatedAt);

public sealed record AllocationResponse(
    Guid WalletId,
    long AmountMinor);
