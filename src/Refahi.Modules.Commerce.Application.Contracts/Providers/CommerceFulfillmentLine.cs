namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceFulfillmentLine(
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity, 
    long UnitPriceMinor, 
    string ProviderPayloadJson
);
