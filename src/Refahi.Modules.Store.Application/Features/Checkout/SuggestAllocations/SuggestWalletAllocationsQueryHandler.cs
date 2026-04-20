using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Store.Application.Features.Checkout.SuggestAllocations;

public class SuggestWalletAllocationsQueryHandler : IRequestHandler<SuggestWalletAllocationsQuery, SuggestAllocationsResponse>
{
    private readonly IMediator _mediator;

    public SuggestWalletAllocationsQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<SuggestAllocationsResponse> Handle(
        SuggestWalletAllocationsQuery request, CancellationToken cancellationToken)
    {
        var wallets = await _mediator.Send(new GetMyWalletsQuery(request.UserId), cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Valid OrgCredit wallets: not expired, allowed for at least one cart category
        var orgCreditWallets = wallets
            .Where(w => w.WalletType == "OrgCredit")
            .Where(w => w.ContractExpiresAt is null || w.ContractExpiresAt > now)
            .Where(w => w.AvailableBalanceMinor > 0)
            .Where(w => IsAllowedForCategories(w.AllowedCategoryCode, request.CartCategoryCodes))
            // Sort by soonest expiry first (null = never expires → last)
            .OrderBy(w => w.ContractExpiresAt ?? DateTimeOffset.MaxValue)
            .ToList();

        // REFAHI wallets
        var refahiWallets = wallets
            .Where(w => w.WalletType == "REFAHI")
            .Where(w => w.AvailableBalanceMinor > 0)
            .ToList();

        // Priority: OrgCredit first, then REFAHI
        var priorityWallets = orgCreditWallets
            .Cast<WalletSummaryDto>()
            .Concat(refahiWallets)
            .ToList();

        var suggestions = new List<AllocationSuggestion>();
        var remaining = request.TotalAmountMinor;

        foreach (var wallet in priorityWallets)
        {
            if (remaining <= 0) break;

            var take = Math.Min(wallet.AvailableBalanceMinor, remaining);
            suggestions.Add(new AllocationSuggestion(
                WalletId: wallet.WalletId,
                WalletType: wallet.WalletType,
                AmountMinor: take,
                AvailableBalanceMinor: wallet.AvailableBalanceMinor));

            remaining -= take;
        }

        var totalSuggested = request.TotalAmountMinor - remaining;

        return new SuggestAllocationsResponse(
            Allocations: suggestions,
            TotalSuggestedMinor: totalSuggested,
            IsCovered: remaining <= 0);
    }

    /// <summary>
    /// Returns true if the wallet (with the given AllowedCategoryCode) is allowed
    /// for at least one of the cart category codes. Uses prefix matching.
    /// If AllowedCategoryCode is null, the wallet is unrestricted.
    /// </summary>
    private static bool IsAllowedForCategories(string? allowedCategoryCode, List<string> cartCategoryCodes)
    {
        if (allowedCategoryCode is null) return true;
        if (cartCategoryCodes.Count == 0) return true;

        return cartCategoryCodes.Any(code =>
            code.StartsWith(allowedCategoryCode, StringComparison.OrdinalIgnoreCase)
            || allowedCategoryCode.StartsWith(code, StringComparison.OrdinalIgnoreCase));
    }
}
