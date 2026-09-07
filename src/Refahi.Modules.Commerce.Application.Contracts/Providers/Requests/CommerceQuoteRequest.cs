namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;

public sealed record CommerceQuoteRequest(
    string ProductKey, 
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity
);
