using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;

/// <summary>
/// Request body for Create Payment Intent endpoint.
/// Idempotency-Key is provided via header.
/// </summary>
public sealed record CreatePaymentIntentRequest(
    Guid OrderId,
    long AmountMinor,
    string Currency,
    List<AllocationRequest> Allocations,
    string? MetadataJson = null);

public sealed record AllocationRequest(
    Guid WalletId,
    long AmountMinor);
