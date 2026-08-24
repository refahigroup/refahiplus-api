namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceQuoteRequest(
    string ProductKey, 
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity
);
