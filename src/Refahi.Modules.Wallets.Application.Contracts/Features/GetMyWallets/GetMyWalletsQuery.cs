using System;
using System.Collections.Generic;
using MediatR;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;

public sealed record GetMyWalletsQuery(Guid UserId) : IRequest<List<WalletSummaryDto>>;

public sealed record WalletSummaryDto(
    Guid WalletId,
    string WalletType,
    string Currency,
    long AvailableBalanceMinor,
    long TotalBalanceMinor,
    long HeldAmountMinor,
    string? AllowedCategoryCode = null,
    DateTimeOffset? ContractExpiresAt = null);
