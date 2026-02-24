namespace Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

/// <summary>
/// Request body for TopUp endpoint.
/// Idempotency-Key is provided via header and is not part of the body.
/// </summary>
public sealed record TopUpWalletRequest(
    long AmountMinor,
    string Currency,
    string? MetadataJson = null,
    string? ExternalReference = null);
