using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

/// <summary>
/// Command: Refund Payment to the original funding wallets.
/// A null AmountMinor preserves the legacy full-refund behavior.
/// </summary>
public sealed record RefundPaymentCommand(
    Guid PaymentId,
    string IdempotencyKey,
    string? Reason,
    string? MetadataJson,
    long? AmountMinor = null
) : IRequest<CommandResponse<RefundPaymentResponse>>;
