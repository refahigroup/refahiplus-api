using System;

namespace Wallets.Application.Contracts.Features.ReleasePaymentIntent;

/// <summary>
/// Response for Release Payment Intent.
/// </summary>
public sealed record ReleasePaymentIntentResponse(
    Guid IntentId,
    Guid OrderId,
    string Status,
    DateTimeOffset ReleasedAt);
