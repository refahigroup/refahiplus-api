namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentFulfillmentLine(
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity, 
    long UnitPriceMinor, 
    string ProviderPayloadJson
);
