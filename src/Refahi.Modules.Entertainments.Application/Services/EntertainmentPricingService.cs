using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Services;

public sealed class EntertainmentPricingService : IEntertainmentPricingService
{
    public EntertainmentPrice Calculate(decimal providerCost, decimal percent, long fixedMinor, string version, decimal discountPercent = 0, long discountFixedMinor = 0)
    {
        if (providerCost <= 0 || decimal.Truncate(providerCost) != providerCost || percent < 0 || fixedMinor < 0 || discountPercent < 0 || discountFixedMinor < 0 || string.IsNullOrWhiteSpace(version))
            throw new EntertainmentDomainException("مبلغ یا قاعده قیمت معتبر نیست", "INVALID_PRICE");
        var cost = checked((long)providerCost);
        var markup = checked((long)Math.Round(providerCost * percent / 100m, 0, MidpointRounding.AwayFromZero) + fixedMinor);
        var discount = checked((long)Math.Round(providerCost * discountPercent / 100m, 0, MidpointRounding.AwayFromZero) + discountFixedMinor);
        var sale = checked(cost + markup - discount);
        if (sale <= 0) throw new EntertainmentDomainException("قیمت نهایی قابل پرداخت نیست", "INVALID_PRICE");
        return new(cost, markup, sale, version) { DiscountMinor = discount };
    }
}
