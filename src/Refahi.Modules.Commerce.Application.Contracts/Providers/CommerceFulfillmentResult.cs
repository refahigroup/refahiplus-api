namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceFulfillmentResult(
    string ProviderOrderCode, 
    IReadOnlyList<CommerceIssuedTicket> Tickets
);
