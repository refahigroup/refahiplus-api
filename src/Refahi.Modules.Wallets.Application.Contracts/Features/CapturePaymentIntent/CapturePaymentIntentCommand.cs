using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;

/// <summary>
/// Command: Capture Payment Intent (finalize payment).
/// </summary>
public sealed record CapturePaymentIntentCommand(
    Guid IntentId,
    string IdempotencyKey)
    : IRequest<CommandResponse<CapturePaymentIntentResponse>>;
