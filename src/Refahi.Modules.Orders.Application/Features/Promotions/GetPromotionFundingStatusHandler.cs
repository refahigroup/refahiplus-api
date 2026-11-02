using MediatR;
using Microsoft.Extensions.Configuration;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
namespace Refahi.Modules.Orders.Application.Features.Promotions;

public sealed class GetPromotionFundingStatusHandler(IConfiguration configuration, IWalletReadRepository wallets) : IRequestHandler<GetPromotionFundingStatusQuery, PromotionFundingStatusDto>
{
    public async Task<PromotionFundingStatusDto> Handle(GetPromotionFundingStatusQuery r, CancellationToken ct)
    {
        var enabled = bool.TryParse(configuration["Promotions:Enabled"], out var flag) && flag;
        if (!Guid.TryParse(configuration["Promotions:FundingWalletId"], out var id) || id == Guid.Empty) return new(enabled, null, false, 0, 0);
        var info = await wallets.GetByIdAsync(id, ct);
        if (info is null || info.WalletType != 1 || info.Status != 1 || info.Currency != "IRR") return new(enabled, id, false, 0, 0);
        var balance = await wallets.GetWalletBalanceAsync(id,ct);
        return new(enabled, id, true, balance.AvailableMinor, balance.PendingMinor);
    }
}
