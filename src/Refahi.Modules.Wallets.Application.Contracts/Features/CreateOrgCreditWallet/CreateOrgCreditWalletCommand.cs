using System;
using MediatR;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CreateOrgCreditWallet;

/// <summary>
/// Command: Admin-only provisioning of an OrgCredit wallet for an organisation/user.
/// </summary>
public sealed record CreateOrgCreditWalletCommand(
    Guid OwnerId,
    string Currency,
    string? AllowedCategoryCode,
    DateTimeOffset? ContractExpiresAt)
    : IRequest<CreateOrgCreditWalletResponse>;

public sealed record CreateOrgCreditWalletResponse(
    Guid WalletId,
    string WalletType,
    string Currency,
    string? AllowedCategoryCode,
    DateTimeOffset? ContractExpiresAt);
