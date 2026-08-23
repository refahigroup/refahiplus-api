namespace Refahi.Modules.Commerce.Application.Contracts.Abstraction;

public interface ICommerceProvider
{
    string Key { get; }


    Task<IEnumerable<Seller>> GetSellersAsync(CancellationToken cancellationToken);
    Task<IEnumerable<Offer>> GetOffersAsync(CancellationToken cancellationToken);
    Task<IEnumerable<Product>> GetProductAsync(CancellationToken cancellationToken);
}
