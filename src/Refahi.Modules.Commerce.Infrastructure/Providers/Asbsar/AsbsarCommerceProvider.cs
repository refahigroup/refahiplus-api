using Refahi.Modules.Commerce.Application.Contracts.Abstraction;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

public class AsbsarCommerceProvider : ICommerceProvider
{
    public string Key => "aabsar";

    public async Task<IEnumerable<Offer>> GetOffersAsync()
    {
        return new List<Offer>()
        {
            new Offer
            {
                ProviderKey = Key
            }
        };
    }

    public async Task<IEnumerable<Seller>> GetSellersAsync()
    {
        throw new NotImplementedException();
    }
}
