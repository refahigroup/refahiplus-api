namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceIssuedTicket(
    string Code, 
    bool IsChild
);
