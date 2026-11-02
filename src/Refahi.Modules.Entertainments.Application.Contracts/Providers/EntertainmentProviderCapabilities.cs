namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentProviderCapabilities(
    bool SupportsCancellation, 
    bool SupportsSafeFulfillmentRetry
);
