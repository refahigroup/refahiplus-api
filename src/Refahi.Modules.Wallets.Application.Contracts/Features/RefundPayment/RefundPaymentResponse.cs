using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

/// <summary>
/// Response for Refund Payment.
/// </summary>
public sealed record RefundPaymentResponse(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    string Status,
    long AmountMinor,
    string Currency,
    List<RefundAllocationResponse> Allocations,
    DateTimeOffset CompletedAt);

public sealed record RefundAllocationResponse(
    Guid WalletId,
    long AmountMinor,
    Guid LedgerEntryId);
