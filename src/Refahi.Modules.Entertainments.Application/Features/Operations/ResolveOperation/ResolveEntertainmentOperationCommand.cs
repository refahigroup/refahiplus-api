using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation;

public sealed record ResolveEntertainmentOperationCommand(
    Guid FulfillmentId, 
    string Outcome, 
    string Evidence,
    string? ProviderOrderCode, 
    IReadOnlyList<ResolvedTicketInput>? Tickets, Guid ActorId, string IdempotencyKey
) : IRequest;