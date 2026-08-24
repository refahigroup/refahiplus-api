using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed record ResolveCommerceOperationCommand(
    Guid FulfillmentId, 
    string Outcome, 
    string Evidence,
    string? ProviderOrderCode, 
    IReadOnlyList<ResolvedTicketInput>? Tickets
) : IRequest;