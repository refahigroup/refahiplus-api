using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

public sealed record TopUpWalletResponse(
    Guid WalletId,
    Guid OperationId,
    Guid LedgerEntryId,
    long AmountMinor,
    string Currency,
    long AvailableBalanceMinor,
    DateTimeOffset CreatedAt);
