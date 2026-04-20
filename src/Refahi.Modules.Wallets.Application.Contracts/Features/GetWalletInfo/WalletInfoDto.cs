using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;

/// <summary>
/// Lightweight projection of a wallet's metadata (no balance).
/// Used internally to enforce OrgCredit business rules.
/// </summary>
public sealed record WalletInfoDto(
    Guid WalletId,
    short WalletType,
    short Status,
    string Currency,
    string? AllowedCategoryCode,
    DateTimeOffset? ContractExpiresAt);
