using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Orders.Application.Features.GetOrderFundingEligibility;

public sealed class GetOrderFundingEligibilityQueryHandler(IMediator mediator, TimeProvider clock)
    : IRequestHandler<GetOrderFundingEligibilityQuery, OrderFundingEligibilityDto?>
{
    public async Task<OrderFundingEligibilityDto?> Handle(
        GetOrderFundingEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        var options = await mediator.Send(
            new GetOrderPaymentOptionsQuery(request.OrderId, request.UserId, "User"),
            cancellationToken);
        if (options is null)
            return null;

        var permittedPersonalWallets = options.Wallets
            .Where(wallet => wallet.IsAllowed && IsPersonalWallet(wallet.WalletType))
            .Select(wallet => wallet.WalletId)
            .Distinct()
            .ToArray();
        if (permittedPersonalWallets.Length > 1)
            throw new InvalidOperationException(
                "بیش از یک کیف‌پول شخصی مجاز برای سفارش یافت شد");
        var permittedPersonalWalletId = permittedPersonalWallets.SingleOrDefault();

        // PaymentOptions is the trusted Orders/Wallet allocation boundary.  Its
        // suggested total is already category/contract/hold aware and capped by
        // the payable amount.  Do not independently add raw wallet balances here:
        // options are not guaranteed to be mutually additive for every Wallet
        // policy now or in the future.
        var totalEligible = options.TotalSuggestedMinor;
        var deficit = options.DeficitMinor;

        if (totalEligible < 0
            || totalEligible > options.FinalAmountMinor
            || deficit < 0
            || deficit > options.FinalAmountMinor
            || deficit != options.FinalAmountMinor - totalEligible
            || options.IsCovered != (deficit == 0))
        {
            throw new InvalidOperationException(
                "خلاصه تأمین مالی سفارش از نظر داخلی سازگار نیست");
        }

        return new OrderFundingEligibilityDto(
            options.OrderId,
            request.UserId,
            options.FinalAmountMinor,
            options.Currency,
            totalEligible,
            options.IsCovered,
            deficit,
            permittedPersonalWalletId != Guid.Empty,
            clock.GetUtcNow(),
            permittedPersonalWalletId == Guid.Empty ? null : permittedPersonalWalletId);
    }

    private static bool IsPersonalWallet(string walletType) =>
        string.Equals(walletType, "REFAHI", StringComparison.OrdinalIgnoreCase)
        || string.Equals(walletType, "Refahi", StringComparison.OrdinalIgnoreCase)
        || string.Equals(walletType, "User", StringComparison.OrdinalIgnoreCase);
}
