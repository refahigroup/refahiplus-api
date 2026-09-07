using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Services;

public sealed class CommercePricingService : ICommercePricingService
{
    public CommercePrice Calculate(decimal providerCost, decimal percent, long fixedMinor, string version)
    {
        if (providerCost < 0 || decimal.Truncate(providerCost) != providerCost || percent < 0 || fixedMinor < 0 || string.IsNullOrWhiteSpace(version))
            throw new CommerceDomainException("مبلغ یا قاعده قیمت معتبر نیست", "INVALID_PRICE");
        var cost = checked((long)providerCost);
        var markup = checked((long)Math.Round(providerCost * percent / 100m, 0, MidpointRounding.AwayFromZero) + fixedMinor);
        return new(cost, markup, checked(cost + markup), version);
    }
}
