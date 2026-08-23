namespace Refahi.Modules.Commerce.Application.Contracts.Abstraction;

public interface ICommerceProvider
{
    string Key { get; }


    Task<IEnumerable<Seller>> GetSellersAsync();
    Task<IEnumerable<Offer>> GetOffersAsync();
}
