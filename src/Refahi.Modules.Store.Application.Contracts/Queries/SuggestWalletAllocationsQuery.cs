using MediatR;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.Store.Application.Contracts.Queries;

public sealed record SuggestWalletAllocationsQuery(
    Guid UserId,
    long TotalAmountMinor,
    List<string> CartCategoryCodes
) : IRequest<SuggestAllocationsResponse>;

public sealed record SuggestAllocationsResponse(
    List<AllocationSuggestion> Allocations,
    long TotalSuggestedMinor,
    bool IsCovered
);

public sealed record AllocationSuggestion(
    Guid WalletId,
    string WalletType,
    long AmountMinor,
    long AvailableBalanceMinor
);
