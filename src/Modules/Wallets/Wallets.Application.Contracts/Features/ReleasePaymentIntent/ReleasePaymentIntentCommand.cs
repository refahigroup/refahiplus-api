using System;
using MediatR;

namespace Wallets.Application.Contracts.Features.ReleasePaymentIntent;

/// <summary>
/// Command: Release/Cancel Payment Intent.
/// </summary>
public sealed record ReleasePaymentIntentCommand(
    Guid IntentId,
    string IdempotencyKey)
    : IRequest<CommandResponse<ReleasePaymentIntentResponse>>;
