using System;
using MediatR;

namespace Wallets.Application.Contracts.Features.CapturePaymentIntent;

/// <summary>
/// Command: Capture Payment Intent (finalize payment).
/// </summary>
public sealed record CapturePaymentIntentCommand(
    Guid IntentId,
    string IdempotencyKey)
    : IRequest<CommandResponse<CapturePaymentIntentResponse>>;
