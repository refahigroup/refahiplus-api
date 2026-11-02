using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Order.CancelOrder;

public sealed record CancelEntertainmentPurchaseCommand(
    Guid UserId, 
    string CallerRole, 
    Guid EntertainmentPurchaseId, 
    string? Reason,
    string IdempotencyKey
) : IRequest<EntertainmentPurchaseDto>;