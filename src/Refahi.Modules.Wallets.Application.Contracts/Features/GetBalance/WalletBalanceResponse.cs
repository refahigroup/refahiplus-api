using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;

public record WalletBalanceResponse(
    Guid WalletId,
    string Currency,
    long AvailableMinor,
    long PendingMinor,
    long Version,
    DateTimeOffset UpdatedAt
);
