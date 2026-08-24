namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed record ResolvedTicketInput(
    string Code, 
    bool IsChild
);


