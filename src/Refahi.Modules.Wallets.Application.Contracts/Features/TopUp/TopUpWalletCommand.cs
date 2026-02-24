using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

/// <summary>
/// Command: TopUp (money-in) for a single wallet.
///
/// Notes:
/// - Amount is minor units (e.g., cents) and must be > 0.
/// - IdempotencyKey is required and scoped per wallet.
/// </summary>
public sealed record TopUpWalletCommand(
    Guid WalletId,
    long AmountMinor,
    string Currency,
    string IdempotencyKey,
    string? MetadataJson = null,
    string? ExternalReference = null)
    : IRequest<CommandResponse<TopUpWalletResponse>>;
