using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;

namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public interface ICommerceProvider
{
    string Key { get; }
    string Name { get; }
    CommerceProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<CommerceSellerDto>> GetSellersAsync(CancellationToken cancellationToken);
    Task<CommerceSellerDto?> GetSellerAsync(string sellerKey, CancellationToken cancellationToken);
    Task<CommerceCatalogPage> GetProductsAsync(CommerceCatalogQuery query, CancellationToken cancellationToken);
    Task<CommerceProductDto?> GetProductAsync(string productKey, CancellationToken cancellationToken);
    Task<CommerceQuoteResult> QuoteAsync(CommerceQuoteRequest request, CancellationToken cancellationToken);
    Task<CommerceFulfillmentResult> FulfillAsync(CommerceFulfillmentRequest request, CancellationToken cancellationToken);
    Task CancelAsync(CommerceCancellationRequest request, CancellationToken cancellationToken);
}
