using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

/// <summary>
/// Command: Refund Payment (full refund to original wallets).
/// </summary>
public sealed record RefundPaymentCommand(
    Guid PaymentId,
    string IdempotencyKey,
    string? Reason,
    string? MetadataJson)
    : IRequest<CommandResponse<RefundPaymentResponse>>;
