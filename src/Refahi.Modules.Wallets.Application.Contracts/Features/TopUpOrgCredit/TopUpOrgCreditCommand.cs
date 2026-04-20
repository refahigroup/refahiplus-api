using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.TopUpOrgCredit;

/// <summary>
/// Command: TopUp an OrgCredit wallet.
/// Validates wallet type and contract validity before delegating to the standard TopUp pipeline.
/// </summary>
public sealed record TopUpOrgCreditCommand(
    Guid WalletId,
    long AmountMinor,
    string Currency,
    string IdempotencyKey,
    string? MetadataJson = null,
    string? ExternalReference = null)
    : IRequest<CommandResponse<TopUpWalletResponse>>;
