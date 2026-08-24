namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceProviderCapabilities(
    bool SupportsCancellation, 
    bool SupportsSafeFulfillmentRetry
);
