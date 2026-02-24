using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;

/// <summary>
/// Response for Capture Payment Intent.
/// </summary>
public sealed record CapturePaymentIntentResponse(
    Guid PaymentId,
    Guid IntentId,
    Guid OrderId,
    long AmountMinor,
    string Currency,
    string Status,
    List<PaymentAllocationResponse> Allocations,
    DateTimeOffset CompletedAt);

public sealed record PaymentAllocationResponse(
    Guid WalletId,
    long AmountMinor,
    Guid LedgerEntryId);
