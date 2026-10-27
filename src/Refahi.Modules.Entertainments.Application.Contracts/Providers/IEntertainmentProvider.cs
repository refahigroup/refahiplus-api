using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public interface IEntertainmentProvider
{
    string Key { get; }
    string Name { get; }
    EntertainmentProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<EntertainmentSupplierDto>> GetSuppliersAsync(CancellationToken cancellationToken);
    Task<EntertainmentSupplierDto?> GetSupplierAsync(string supplierKey, CancellationToken cancellationToken);
    Task<EntertainmentCatalogPage> GetProgramsAsync(EntertainmentCatalogQuery query, CancellationToken cancellationToken);
    Task<EntertainmentProgramDto?> GetProgramAsync(string programKey, CancellationToken cancellationToken);
    Task<EntertainmentQuoteResult> QuoteAsync(EntertainmentQuoteRequest request, CancellationToken cancellationToken);
    Task<EntertainmentFulfillmentResult> FulfillAsync(EntertainmentFulfillmentRequest request, CancellationToken cancellationToken);
    Task CancelAsync(EntertainmentCancellationRequest request, CancellationToken cancellationToken);
}
