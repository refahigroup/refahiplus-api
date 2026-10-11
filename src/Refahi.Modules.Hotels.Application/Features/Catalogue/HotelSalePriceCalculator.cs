using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelSalePriceCalculator : IHotelSalePriceCalculator
{
    public int Version => 1;
    public long CalculateFinalPrice(ProviderHotelOffer offer) => offer.DisplayPriceMinor > 0 ? offer.DisplayPriceMinor
        : throw new InvalidOperationException("قیمت نمایش هتل معتبر نیست.");
}
