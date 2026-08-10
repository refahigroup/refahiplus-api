using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;

namespace Refahi.Modules.Store.Application.Services;

public sealed class StoreInPersonFinancialPlanner(
    IMediator mediator,
    IOptions<StorePaymentDistributionOptions> options
) : IStoreInPersonFinancialPlanner
{
    public async Task<StoreInPersonFinancialPlan> BuildAsync(
        Guid supplierId,
        long grossAmountMinor,
        decimal commissionPercent,
        bool vatApplicable,
        CancellationToken ct
    )
    {
        if (grossAmountMinor <= 0)
            throw new StoreDomainException(
                "مبلغ فروش باید بیشتر از صفر باشد",
                "INVALID_MANUAL_AMOUNT"
            );
        var settings = options.Value;
        var commission = Percentage(grossAmountMinor, commissionPercent);
        var vatPercent = vatApplicable ? settings.VatRatePercent : 0m;
        var vat = Percentage(commission, vatPercent);
        var net = grossAmountMinor - commission - vat;
        if (net < 0)
            throw new StoreDomainException(
                "مجموع کارمزد و مالیات از مبلغ فروش بیشتر است",
                "INVALID_FINANCIAL_BREAKDOWN"
            );

        var providerWallet =
            (await mediator.Send(new GetMyWalletsQuery(supplierId), ct)).SingleOrDefault(x =>
                x.WalletType.Equals("Provider", StringComparison.OrdinalIgnoreCase)
                && x.Currency == "IRR"
            )
            ?? throw new StoreDomainException(
                "کیف درآمد فروشنده یافت نشد",
                "PROVIDER_WALLET_NOT_FOUND"
            );

        var postings = new List<OrderPaymentPostingInput>
        {
            new(providerWallet.WalletId, 1, grossAmountMinor, "store.vendor-gross"),
        };
        if (commission > 0)
        {
            var revenueWallet = await RequireSystemWalletAsync(settings.RefahiRevenueWalletId, ct);
            postings.Add(new(providerWallet.WalletId, 2, commission, "store.commission"));
            postings.Add(new(revenueWallet.WalletId, 1, commission, "store.platform-revenue"));
        }
        if (vat > 0)
        {
            var vatWallet = await RequireSystemWalletAsync(settings.RefahiVatWalletId, ct);
            postings.Add(new(providerWallet.WalletId, 2, vat, "store.vat"));
            postings.Add(new(vatWallet.WalletId, 1, vat, "store.platform-vat"));
        }

        return new StoreInPersonFinancialPlan(
            grossAmountMinor,
            commissionPercent,
            commission,
            vatPercent,
            vat,
            net,
            providerWallet.WalletId,
            postings
        );
    }

    private async Task<WalletInfoDto> RequireSystemWalletAsync(Guid walletId, CancellationToken ct)
    {
        var wallet =
            await mediator.Send(new GetWalletInfoByIdQuery(walletId), ct)
            ?? throw new StoreDomainException(
                "کیف سیستمی پرداخت حضوری یافت نشد",
                "SYSTEM_WALLET_NOT_FOUND"
            );
        if (wallet.WalletType != 1 || wallet.Status != 1 || wallet.Currency != "IRR")
            throw new StoreDomainException(
                "کیف سیستمی پرداخت حضوری معتبر یا فعال نیست",
                "INVALID_SYSTEM_WALLET"
            );
        return wallet;
    }

    private static long Percentage(long amount, decimal percent) =>
        checked((long)Math.Round(amount * percent / 100m, 0, MidpointRounding.AwayFromZero));
}
