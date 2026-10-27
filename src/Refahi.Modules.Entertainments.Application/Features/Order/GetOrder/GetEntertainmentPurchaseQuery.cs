using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Order.GetOrder;

public sealed record GetEntertainmentPurchaseQuery(
    Guid UserId,
    string CallerRole,
    Guid EntertainmentPurchaseId,
    bool RevealTickets = false
) : IRequest<EntertainmentPurchaseDto?>;




