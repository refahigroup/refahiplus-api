namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

public sealed record EntertainmentQuoteRequest(
    string ProgramKey, 
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity
);
