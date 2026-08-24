namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceQuoteResult(
    string ProviderKey, 
    string SellerKey, 
    string ProductKey, 
    string OfferKey,
    string PurchaseOptionKey, 
    string ProductTitle, 
    string OfferTitle, 
    string OptionTitle, 
    string CategoryCode,
    int Quantity, 
    long UnitPriceMinor, 
    int? Capacity, 
    string ProviderPayloadJson
);
