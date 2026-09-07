namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed record PrepareCommerceCheckoutResponse(
    Guid CommerceOrderId, 
    Guid OrderId, 
    string OrderNumber,
    long FinalAmountMinor, 
    string CheckoutDestination
);



