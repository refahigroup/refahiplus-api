namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;

public sealed record PrepareEntertainmentCheckoutResponse(
    Guid EntertainmentPurchaseId, 
    Guid OrderId, 
    string OrderNumber,
    long FinalAmountMinor, 
    string CheckoutDestination
);



