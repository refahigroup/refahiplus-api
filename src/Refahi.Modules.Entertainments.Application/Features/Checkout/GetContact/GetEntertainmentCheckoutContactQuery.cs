using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.GetContact;

public sealed record GetEntertainmentCheckoutContactQuery(
    Guid UserId
) : IRequest<EntertainmentCheckoutContactDto>;