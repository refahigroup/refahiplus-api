using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public interface IEntertainmentProvider
{
    string Key { get; }
    string Name { get; }
    EntertainmentProviderCapabilities Capabilities { get; }

    Task<EntertainmentQuoteResult> QuoteAsync(EntertainmentQuoteRequest request, CancellationToken cancellationToken);
    Task<EntertainmentFulfillmentResult> FulfillAsync(EntertainmentFulfillmentRequest request, CancellationToken cancellationToken);
    Task CancelAsync(EntertainmentCancellationRequest request, CancellationToken cancellationToken);
}
